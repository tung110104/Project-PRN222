using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface ISettingsService
{
    Task<int> GetIntAsync(string key, int defaultValue);
    Task<decimal> GetDecimalAsync(string key, decimal defaultValue);
    Task<List<SystemSetting>> GetAllAsync();
    Task UpdateAsync(string key, string value);

    /// <summary>Hạng thành viên theo tổng điểm tích lũy trọn đời (mục 5).</summary>
    Task<(string TierName, int DiscountPercent)> GetTierAsync(int lifetimePoints);
}

public class SettingsService : ISettingsService
{
    private readonly IUnitOfWork _uow;
    public SettingsService(IUnitOfWork uow) => _uow = uow;

    public async Task<int> GetIntAsync(string key, int defaultValue)
    {
        var s = await _uow.SystemSettings.Query()
            .FirstOrDefaultAsync(x => x.SettingKey == key);
        return s != null && int.TryParse(s.SettingValue, out var v) ? v : defaultValue;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue)
    {
        var s = await _uow.SystemSettings.Query()
            .FirstOrDefaultAsync(x => x.SettingKey == key);
        return s != null && decimal.TryParse(s.SettingValue, out var v) ? v : defaultValue;
    }

    public Task<List<SystemSetting>> GetAllAsync() =>
        _uow.SystemSettings.Query().OrderBy(s => s.SettingKey).ToListAsync();

    public async Task UpdateAsync(string key, string value)
    {
        var s = await _uow.SystemSettings.Query()
            .FirstOrDefaultAsync(x => x.SettingKey == key);
        if (s == null)
        {
            await _uow.SystemSettings.AddAsync(new SystemSetting
            {
                SettingKey = key,
                SettingValue = value
            });
        }
        else
        {
            s.SettingValue = value;
            _uow.SystemSettings.Update(s);
        }
        await _uow.SaveChangesAsync();
    }

    public async Task<(string TierName, int DiscountPercent)> GetTierAsync(int lifetimePoints)
    {
        var diamond = await GetIntAsync("TierDiamondPoints", 5000);
        var gold = await GetIntAsync("TierGoldPoints", 2000);
        var silver = await GetIntAsync("TierSilverPoints", 500);

        if (lifetimePoints >= diamond)
            return ("Kim cương", await GetIntAsync("TierDiamondDiscount", 10));
        if (lifetimePoints >= gold)
            return ("Vàng", await GetIntAsync("TierGoldDiscount", 5));
        if (lifetimePoints >= silver)
            return ("Bạc", await GetIntAsync("TierSilverDiscount", 3));
        return ("Thường", 0);
    }
}
