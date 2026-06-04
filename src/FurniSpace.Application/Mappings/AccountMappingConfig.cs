using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Domain.Entities;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class AccountMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Account, AccountDto>()
            .Map(destination => destination.Status, source => source.Status.HasValue ? source.Status.Value.ToString() : null);
    }
}
