using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SportsFieldBooking.Web.Services;

/// <summary>
/// Tich hop cong thanh toan VNPay (sandbox) theo chuan vnp_ v2.1.0:
/// build URL redirect co chu ky HMAC-SHA512, va xac thuc chu ky khi VNPay redirect ve.
/// Dang ky merchant sandbox mien phi tai https://sandbox.vnpayment.vn/devreg/ de lay TmnCode + HashSecret.
/// Chua cau hinh (TmnCode trong) -> he thong tu dung cong demo noi bo (PaymentGateway/Demo).
/// </summary>
public interface IVnPayService
{
    bool IsConfigured { get; }
    string CreatePaymentUrl(HttpContext http, decimal amount, string txnRef, string orderInfo, string returnUrl);
    /// <summary>Xac thuc chu ky + trang thai giao dich tren query VNPay tra ve.</summary>
    (bool ValidSignature, bool Success, string TxnRef, decimal Amount, string ResponseCode) ValidateReturn(IQueryCollection query);
}

public class VnPayService : IVnPayService
{
    private readonly IConfiguration _config;
    public VnPayService(IConfiguration config) => _config = config;

    private string TmnCode => _config["VnPay:TmnCode"] ?? "";
    private string HashSecret => _config["VnPay:HashSecret"] ?? "";
    private string BaseUrl => _config["VnPay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TmnCode) && !string.IsNullOrWhiteSpace(HashSecret);

    public string CreatePaymentUrl(HttpContext http, decimal amount, string txnRef, string orderInfo, string returnUrl)
    {
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        if (ip == "::1") ip = "127.0.0.1";

        // Cac tham so bat buoc theo tai lieu VNPay; vnp_Amount nhan 100 (don vi = dong x100)
        var data = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = TmnCode,
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(),
            ["vnp_CreateDate"] = DateTime.Now.ToString("yyyyMMddHHmmss"),
            ["vnp_ExpireDate"] = DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = ip,
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_TxnRef"] = txnRef
        };

        // Chuoi ky = cac cap key=value (URL-encoded) sap xep alphabet, noi bang '&'
        var raw = string.Join("&", data.Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}"));
        var secureHash = HmacSha512(HashSecret, raw);
        return $"{BaseUrl}?{raw}&vnp_SecureHash={secureHash}";
    }

    public (bool ValidSignature, bool Success, string TxnRef, decimal Amount, string ResponseCode) ValidateReturn(IQueryCollection query)
    {
        var receivedHash = query["vnp_SecureHash"].ToString();
        var data = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in query)
        {
            if (key.StartsWith("vnp_") && key is not ("vnp_SecureHash" or "vnp_SecureHashType"))
                data[key] = value.ToString();
        }

        var raw = string.Join("&", data.Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}"));
        var computedHash = HmacSha512(HashSecret, raw);
        var valid = string.Equals(computedHash, receivedHash, StringComparison.OrdinalIgnoreCase);

        var responseCode = query["vnp_ResponseCode"].ToString();
        var success = valid && responseCode == "00" && query["vnp_TransactionStatus"].ToString() == "00";
        var amount = long.TryParse(query["vnp_Amount"], out var a) ? a / 100m : 0m;

        return (valid, success, query["vnp_TxnRef"].ToString(), amount, responseCode);
    }

    private static string HmacSha512(string key, string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }
}
