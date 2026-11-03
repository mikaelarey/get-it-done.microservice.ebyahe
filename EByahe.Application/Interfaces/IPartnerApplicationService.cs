using EByahe.Application.Dtos.Requests;
using EByahe.Application.Dtos.Responses;

namespace EByahe.Application.Interfaces;

public interface IPartnerApplicationService
{
  Task<CustomerPartnerApplicationPage> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default);
  Task<CustomerPartnerApplicationDetails?> GetAsync(Guid id, string userId, CancellationToken cancellationToken = default);
  Task<CustomerPartnerDocumentContent?> GetDocumentAsync(Guid id, Guid documentId, string userId,
    CancellationToken cancellationToken = default);
  Task<PartnerApplicationResponse> SubmitAsync(CreatePartnerApplicationRequest request, string createdBy,
    CancellationToken cancellationToken = default);
}
