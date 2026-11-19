using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
namespace EByahe.Infrastructure.Persistence;
public sealed partial class EByaheDbContext
{
  private void ResolveBank(PartnerApplication application)
  {
    if (application.BankName.Length == 0) { application.Bank = null; application.CodeDecodeBank = null; return; }
    var name = application.BankName;
    var bank = CodeDecodeBanks.SingleOrDefault(bank => bank.Decode == name)
      ?? throw new InvalidOperationException("Select a configured bank.");
    application.Bank = bank; application.CodeDecodeBank = bank.Code;
  }
  private async Task ResolveBankAsync(PartnerApplication application, CancellationToken ct)
  {
    if (application.BankName.Length == 0) { application.Bank = null; application.CodeDecodeBank = null; return; }
    var name = application.BankName;
    var bank = await CodeDecodeBanks.SingleOrDefaultAsync(bank => bank.Decode == name, ct)
      ?? throw new InvalidOperationException("Select a configured bank.");
    application.Bank = bank; application.CodeDecodeBank = bank.Code;
  }
}
