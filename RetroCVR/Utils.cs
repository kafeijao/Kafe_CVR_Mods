using System.Security.Cryptography;
using System.Text;

namespace Kafe.RetroCVR;

public static class Utils {

    public static string CreateMD5(byte[] inputBytes) {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(inputBytes);
        var sb = new StringBuilder();
        foreach (var t in hashBytes) {
            sb.Append(t.ToString("X2"));
        }
        return sb.ToString();
    }

}
