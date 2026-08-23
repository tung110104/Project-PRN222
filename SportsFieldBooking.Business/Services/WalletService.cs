using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IWalletService
{
    Task<Wallet> GetOrCreateAsync(int userId);
    Task<List<WalletTransaction>> GetTransactionsAsync(int userId);
    Task<(bool Success, string Message)> DepositAsync(int userId, decimal amount);
    /// <summary>Tru tien vi de thanh toan booking. Goi ben trong PaymentService (da co transaction bao ngoai).</summary>
    Task<(bool Success, string Message)> ChargeAsync(int userId, decimal amount, int bookingId, string description);
    /// <summary>Hoan tien vao vi (huy booking / san bao tri).</summary>
    Task RefundAsync(int userId, decimal amount, int? bookingId, string description);
    /// <summary>Cong tien thuong (cashback khi thanh toan bang vi).</summary>
    Task CashbackAsync(int userId, decimal amount, int bookingId, string description);
    // ----- Admin -----
    Task<List<Wallet>> GetAllForAdminAsync();
    Task<(bool Success, string Message)> AdjustAsync(int userId, decimal amount, string reason);
    Task ToggleLockAsync(int walletId);
}

public class WalletService : IWalletService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;

    public WalletService(IUnitOfWork uow, IEmailService emailService, INotificationService notificationService)
    {
        _uow = uow;
        _emailService = emailService;
        _notificationService = notificationService;
    }

    /// <summary>Goi NotifyAsync nhung nuot loi - thong bao hong khong duoc lam hong giao dich vi.</summary>
    private async Task TryNotifyAsync(int userId, string title, string message, string? url = null)
    {
        try { await _notificationService.NotifyAsync(userId, title, message, url); }
        catch { /* bo qua */ }
    }

    public async Task<Wallet> GetOrCreateAsync(int userId)
    {
        var wallet = await _uow.Wallets.Query().FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet != null) return wallet;

        wallet = new Wallet { UserId = userId, Balance = 0, UpdatedAt = DateTime.Now };
        await _uow.Wallets.AddAsync(wallet);
        await _uow.SaveChangesAsync();
        return wallet;
    }

    public Task<List<WalletTransaction>> GetTransactionsAsync(int userId) =>
        _uow.WalletTransactions.Query()
            .Where(t => t.Wallet.UserId == userId)
            .OrderByDescending(t => t.WalletTransactionId)
            .ToListAsync();

    // Moi bien dong so du deu tao 1 dong WalletTransaction kem BalanceAfter de doi soat
    private async Task AddTransactionAsync(Wallet wallet, string type, decimal amount, string? description, int? bookingId)
    {
        wallet.Balance += amount;
        wallet.UpdatedAt = DateTime.Now;
        _uow.Wallets.Update(wallet);
        await _uow.WalletTransactions.AddAsync(new WalletTransaction
        {
            WalletId = wallet.WalletId,
            Type = type,
            Amount = amount,
            BalanceAfter = wallet.Balance,
            Description = description,
            BookingId = bookingId,
            CreatedAt = DateTime.Now
        });
    }

    public async Task<(bool Success, string Message)> DepositAsync(int userId, decimal amount)
    {
        var min = AppConfigSingleton.Instance.MinDepositAmount;
        if (amount < min) return (false, $"Số tiền nạp tối thiểu là {min:N0}đ.");

        var wallet = await GetOrCreateAsync(userId);
        if (wallet.IsLocked) return (false, "Ví của bạn đang bị khóa. Vui lòng liên hệ quản trị viên.");

        await AddTransactionAsync(wallet, "Deposit", amount, "Nạp tiền vào ví qua cổng thanh toán", null);

        // Khuyen mai nap: dat muc thi tang them tien vao vi (giao dich "Bonus" rieng de doi soat)
        var bonusTier = AppConfigSingleton.Instance.GetDepositBonus(amount);
        if (bonusTier != null)
            await AddTransactionAsync(wallet, "Bonus", bonusTier.BonusAmount,
                $"Khuyến mãi nạp: nạp từ {bonusTier.MinAmount:N0}đ tặng {bonusTier.BonusAmount:N0}đ", null);

        await _uow.SaveChangesAsync();

        // Email bien lai nap tien
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user != null)
        {
            var bonusRow = bonusTier == null ? ""
                : $"<tr><td>Khuyến mãi nạp</td><td><b style=\"color:#198754\">+{bonusTier.BonusAmount:N0}đ</b></td></tr>";
            try
            {
                await _emailService.SendAsync(user.Email,
                    $"[SportBooking] Nạp ví thành công {amount:N0}đ",
                    $"""
                    <h3>Xin chào {user.FullName},</h3>
                    <p>Bạn đã nạp tiền vào ví SportBooking thành công.</p>
                    <div style="border:2px solid #198754;border-radius:8px;padding:16px;margin:16px 0;text-align:center;">
                        <p style="margin:0;">Số tiền nạp</p>
                        <h2 style="color:#198754;margin:4px 0;">{amount:N0}đ</h2>
                    </div>
                    <table cellpadding="6" style="border-collapse:collapse;">
                        {bonusRow}
                        <tr><td>Số dư ví hiện tại</td><td><b>{wallet.Balance:N0}đ</b></td></tr>
                        <tr><td>Thời gian</td><td>{DateTime.Now:HH:mm dd/MM/yyyy}</td></tr>
                    </table>
                    <p>Tiền trong ví dùng để đặt sân, không rút ra được. Xem lịch sử giao dịch tại mục <b>Ví &amp; Điểm</b>.</p>
                    <p>SportBooking - Hệ thống đặt sân thể thao</p>
                    """);
            }
            catch { /* loi gui mail khong duoc lam hong giao dich nap tien */ }
        }

        // Bao khach: bien lai nap tien tren chuong thong bao (huu ich khi quay ve tu cong thanh toan)
        var bonusText = bonusTier == null ? "" : $" + tặng {bonusTier.BonusAmount:N0}đ khuyến mãi nạp";
        await TryNotifyAsync(userId,
            "Nạp ví thành công",
            $"Đã nạp {amount:N0}đ vào ví{bonusText}. Số dư hiện tại: {wallet.Balance:N0}đ.",
            "/Wallet/Index");

        return (true, bonusTier == null
            ? $"Đã nạp {amount:N0}đ vào ví."
            : $"Đã nạp {amount:N0}đ vào ví + tặng {bonusTier.BonusAmount:N0}đ khuyến mãi nạp.");
    }

    public async Task<(bool Success, string Message)> ChargeAsync(int userId, decimal amount, int bookingId, string description)
    {
        var wallet = await GetOrCreateAsync(userId);
        if (wallet.IsLocked) return (false, "Ví đang bị khóa, không thể thanh toán bằng ví.");
        if (wallet.Balance < amount)
            return (false, $"Số dư ví không đủ (hiện có {wallet.Balance:N0}đ, cần {amount:N0}đ). Vui lòng nạp thêm.");

        await AddTransactionAsync(wallet, "Payment", -amount, description, bookingId);
        await _uow.SaveChangesAsync();
        return (true, "Đã trừ tiền ví.");
    }

    public async Task RefundAsync(int userId, decimal amount, int? bookingId, string description)
    {
        if (amount <= 0) return;
        var wallet = await GetOrCreateAsync(userId);
        await AddTransactionAsync(wallet, "Refund", amount, description, bookingId);
        await _uow.SaveChangesAsync();
    }

    public async Task CashbackAsync(int userId, decimal amount, int bookingId, string description)
    {
        if (amount <= 0) return;
        var wallet = await GetOrCreateAsync(userId);
        await AddTransactionAsync(wallet, "Cashback", amount, description, bookingId);
        await _uow.SaveChangesAsync();

        // Bao khach: tien cashback da ve vi
        await TryNotifyAsync(userId,
            "Nhận cashback",
            $"Bạn được hoàn {amount:N0}đ vào ví ({description}). Số dư hiện tại: {wallet.Balance:N0}đ.",
            "/Wallet/Index");
    }

    public Task<List<Wallet>> GetAllForAdminAsync() =>
        _uow.Wallets.Query().Include(w => w.User).OrderBy(w => w.UserId).ToListAsync();

    public async Task<(bool Success, string Message)> AdjustAsync(int userId, decimal amount, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Cộng/trừ tiền thủ công bắt buộc phải ghi lý do.");
        var wallet = await GetOrCreateAsync(userId);
        if (wallet.Balance + amount < 0)
            return (false, "Không thể trừ quá số dư hiện tại của ví.");

        await AddTransactionAsync(wallet, "Adjust", amount, $"Admin điều chỉnh: {reason}", null);
        await _uow.SaveChangesAsync();

        // Bao khach: admin vua cong/tru tien vi (thao tac don phuong thi nguoi bi anh huong phai duoc bao)
        await TryNotifyAsync(userId,
            "Điều chỉnh số dư ví",
            $"Quản trị viên đã {(amount >= 0 ? "cộng" : "trừ")} {Math.Abs(amount):N0}đ " +
            $"{(amount >= 0 ? "vào" : "khỏi")} ví của bạn — lý do: {reason}. Số dư hiện tại: {wallet.Balance:N0}đ.",
            "/Wallet/Index");

        return (true, "Đã điều chỉnh số dư ví.");
    }

    public async Task ToggleLockAsync(int walletId)
    {
        var wallet = await _uow.Wallets.GetByIdAsync(walletId);
        if (wallet == null) return;
        wallet.IsLocked = !wallet.IsLocked;
        _uow.Wallets.Update(wallet);
        await _uow.SaveChangesAsync();

        // Bao khach: trang thai vi thay doi (khoa -> khong nap/thanh toan bang vi duoc)
        await TryNotifyAsync(wallet.UserId,
            wallet.IsLocked ? "Ví đã bị khóa" : "Ví đã được mở khóa",
            wallet.IsLocked
                ? "Ví của bạn đã bị quản trị viên khóa — không thể nạp tiền hoặc thanh toán bằng ví. Vui lòng liên hệ quản trị viên nếu cần hỗ trợ."
                : "Ví của bạn đã được mở khóa, có thể nạp tiền và thanh toán bằng ví bình thường.",
            "/Wallet/Index");
    }
}
