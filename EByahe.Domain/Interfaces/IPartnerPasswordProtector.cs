namespace EByahe.Domain.Interfaces;
public interface IPartnerPasswordProtector
{
  string Generate();
  string Protect(Guid requestId, string password);
  string Unprotect(Guid requestId, string password);
}
