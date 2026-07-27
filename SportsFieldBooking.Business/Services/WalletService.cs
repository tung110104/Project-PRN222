using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IWalletService
{
    Task<Wallet> GetOrCreateAsync(int userId);
    Task<List<WalletTransaction>> GetHistoryAsync(int userId);

    /// <summary>Nạp tiền (giả lập cổng thanh toán). Tự cộng bonus nếu đạt ngưỡng khuyến mãi nạp.</summary>
    Task<(bool Success, string Message)> DepositAsync(int userId, decimal amount);

    /// <summary>Trừ ví trả tiền booking. Trả false nếu ví khóa / không đủ số dư.</summary>
    Task<(bool Success, string Message)> PayFromWalletAsync(int userId, Booking booking);

    /// <summary>Hoàn tiền vào ví (hủy booking hợp lệ / sân bảo trì).</summary>
    Task RefundToWalletAsync(int userId, decimal amount, int? bookingId, string note);

    /// <summary>Cashback X% (cấu hình theo sân) khi trả bằng ví.</summary>
    Task CashbackAsync(int userId, Booking booking, int cashbackPercent);

    // Admin (mục 4.7)
    Task<List<Wallet>> GetAllWalletsAsync();
    Task<(bool Success, string Message)> AdjustAsync(int userId, decimal amount, string reason);
    Task ToggleLockAsync(int userId);
}

public class WalletService : IWalletService
{
    private readonly IUnitOfWork _uow;
    private readonly ISettingsService _settings;

    public WalletService(IUnitOfWork uow, ISettingsService settings)
    {
        _uow = uow;
        _settings = settings;
    }

    public async Task<Wallet> GetOrCreateAsync(int userId)
    {
        var wallet = await _uow.Wallets.Query()
            .FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet != null) return wallet;

        wallet = new Wallet { UserId = userId, Balance = 0, UpdatedAt = DateTime.Now };
        await _uow.Wallets.AddAsync(wallet);
        await _uow.SaveChangesAsync();
        return wallet;
    }

    public Task<List<WalletTransaction>> GetHistoryAsync(int userId) =>
        _uow.WalletTransactions.Query()
            .Include(t => t.Wallet)
            .Where(t => t.Wallet.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    /// <summary>Nguyên tắc: KHÔNG BAO GIỜ sửa Balance mà không ghi WalletTransaction.</summary>
    private async Task ApplyAsync(Wallet wallet, string type, decimal amount, int? bookingId, string? note)
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
            BookingId = bookingId,
            Note = note,
            CreatedAt = DateTime.Now
        });
    }

    public async Task<(bool Success, string Message)> DepositAsync(int userId, decimal amount)
    {
        if (amount < 10000) return (false, "Số tiền nạp tối thiểu 10.000đ.");
        if (amount > 50000000) return (false, "Số tiền nạp tối đa 50.000.000đ.");

        var wallet = await GetOrCreateAsync(userId);
        if (wallet.IsLocked) return (false, "Ví của bạn đang bị khóa.");

        await ApplyAsync(wallet, "Deposit", amount, null, "Nạp tiền vào ví (giả lập)");

        // Khuyến mãi nạp (mục 4.5): nạp đủ ngưỡng thì tặng bonus
        var threshold = await _settings.GetDecimalAsync("DepositBonusThreshold", 500000);
        var bonus = await _settings.GetDecimalAsync("DepositBonusAmount", 50000);
        var bonusMsg = "";
        if (threshold > 0 && bonus > 0 && amount >= threshold)
        {
            await ApplyAsync(wallet, "Bonus", bonus, null,
                $"Khuyến mãi nạp: nạp từ {threshold:N0}đ tặng {bonus:N0}đ");
            bonusMsg = $" Bạn được tặng thêm {bonus:N0}đ.";
        }

        await _uow.SaveChangesAsync();
        return (true, $"Đã nạp {amount:N0}đ vào ví.{bonusMsg}");
    }

    public async Task<(bool Success, string Message)> PayFromWalletAsync(int userId, Booking booking)
    {
        var wallet = await GetOrCreateAsync(userId);
        if (wallet.IsLocked) return (false, "Ví của bạn đang bị khóa.");
        if (wallet.Balance < booking.TotalAmount)
            return (false, $"Số dư ví không đủ (còn {wallet.Balance:N0}đ, cần {booking.TotalAmount:N0}đ).");

        await ApplyAsync(wallet, "Payment", -booking.TotalAmount, booking.BookingId,
            $"Thanh toán booking #{booking.BookingId}");
        await _uow.SaveChangesAsync();
        return (true, "Đã trừ ví thành công.");
    }

    public async Task RefundToWalletAsync(int userId, decimal amount, int? bookingId, string note)
    {
        if (amount <= 0) return;
        // Hoàn tiền luôn được phép kể cả sân đã tắt nhận ví — cờ chỉ chặn thanh toán MỚI
        var wallet = await GetOrCreateAsync(userId);
        await ApplyAsync(wallet, "Refund", amount, bookingId, note);
        await _uow.SaveChangesAsync();
    }

    public async Task CashbackAsync(int userId, Booking booking, int cashbackPercent)
    {
        if (cashbackPercent <= 0) return;
        var amount = Math.Round(booking.TotalAmount * cashbackPercent / 100m, 0);
        if (amount <= 0) return;

        var wallet = await GetOrCreateAsync(userId);
        await ApplyAsync(wallet, "Cashback", amount, booking.BookingId,
            $"Cashback {cashbackPercent}% booking #{booking.BookingId}");
        await _uow.SaveChangesAsync();
    }

    // ---------------- Admin (mục 4.7) ----------------

    public Task<List<Wallet>> GetAllWalletsAsync() =>
        _uow.Wallets.Query().Include(w => w.User).OrderBy(w => w.UserId).ToListAsync();

    public async Task<(bool Success, string Message)> AdjustAsync(int userId, decimal amount, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Phải nhập lý do điều chỉnh.");

        var wallet = await GetOrCreateAsync(userId);
        if (wallet.Balance + amount < 0)
            return (false, "Điều chỉnh làm số dư âm — không hợp lệ.");

        await ApplyAsync(wallet, "Adjust", amount, null, $"Admin điều chỉnh: {reason}");
        await _uow.SaveChangesAsync();
        return (true, $"Đã điều chỉnh {amount:N0}đ cho ví user #{userId}.");
    }

    public async Task ToggleLockAsync(int userId)
    {
        var wallet = await GetOrCreateAsync(userId);
        wallet.IsLocked = !wallet.IsLocked;
        wallet.UpdatedAt = DateTime.Now;
        _uow.Wallets.Update(wallet);
        await _uow.SaveChangesAsync();
    }
}
