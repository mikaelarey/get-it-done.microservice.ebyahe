using System.Security.Cryptography;
using System.Text;
using EByahe.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
namespace EByahe.Infrastructure.Messaging;

public sealed class PartnerPasswordProtector(IConfiguration configuration) : IPartnerPasswordProtector
{
  private byte[] Key()
  {
    var key = Convert.FromBase64String(configuration["PartnerMembers:PasswordEncryptionKey"] ?? "");
    return key.Length == 32 ? key : throw new InvalidOperationException("Configure PartnerMembers:PasswordEncryptionKey in both services using secrets.");
  }
  public string Generate()
  {
    const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghjkmnpqrstuvwxyz", digits = "23456789", special = "!@#$%-_";
    var alphabet = upper + lower + digits + special;
    var value = new char[10]; value[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
    value[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)]; value[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
    value[3] = special[RandomNumberGenerator.GetInt32(special.Length)];
    for (var i = 4; i < value.Length; i++) value[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    RandomNumberGenerator.Shuffle(value.AsSpan()); return new string(value);
  }
  public string Protect(Guid requestId, string password)
  {
    var nonce = RandomNumberGenerator.GetBytes(12); var bytes = Encoding.UTF8.GetBytes(password);
    var cipher = new byte[bytes.Length]; var tag = new byte[16]; var key = Key();
    try { using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, bytes, cipher, tag, Encoding.UTF8.GetBytes(requestId.ToString())); }
    finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(bytes); }
    return string.Join('.', "v1", Convert.ToBase64String(nonce), Convert.ToBase64String(cipher), Convert.ToBase64String(tag));
  }
  public string Unprotect(Guid requestId, string password)
  {
    var parts = password.Split('.'); if (parts.Length != 4 || parts[0] != "v1") throw new InvalidDataException();
    var bytes = new byte[Convert.FromBase64String(parts[2]).Length]; var key = Key();
    try { using var aes = new AesGcm(key, 16); aes.Decrypt(Convert.FromBase64String(parts[1]), Convert.FromBase64String(parts[2]),
      Convert.FromBase64String(parts[3]), bytes, Encoding.UTF8.GetBytes(requestId.ToString())); return Encoding.UTF8.GetString(bytes); }
    finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(bytes); }
  }
}
