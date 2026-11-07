using EByahe.Domain.DataModels;

namespace EByahe.Domain.Exceptions;

public sealed class ActivePartnerApplicationException(ActivePartnerApplication? application)
  : Exception("You already have an active partner application. You can submit another after it is approved or declined.")
{
  public ActivePartnerApplication? ActiveApplication { get; } = application;
}
