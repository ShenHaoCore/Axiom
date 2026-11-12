// 通用枚举字典应用服务：唯一键冲突、引用检查。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.ObjectMapping;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public sealed class ReferenceEnumAppService(
    IReferenceEnumRepository repository,
    IGeoDivisionRepository geoDivisionRepository) : IReferenceEnumAppService
{
    public async Task<PagedResponse<ReferenceEnumDto>> ListAsync(
        ReferenceEnumListQuery query,
        CancellationToken ct = default)
    {
        var category = string.IsNullOrWhiteSpace(query.Category)
            ? null
            : ReferenceEnum.NormalizeKey(query.Category, nameof(query.Category));

        var paged = await repository.ListAsync(
            category,
            query.Page,
            Math.Clamp(query.PageSize, 1, 200),
            ct);

        return new PagedResponse<ReferenceEnumDto>(
            paged.Items.Select(ReferenceEnumMapper.ToDto).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize);
    }

    public async Task<ReferenceEnumDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct);
        return entity is null ? null : ReferenceEnumMapper.ToDto(entity);
    }

    public async Task<(ReferenceEnumDto Dto, Uri LocationUri)> CreateAsync(
        CreateReferenceEnumInput input,
        CancellationToken ct = default)
    {
        var entity = ReferenceEnum.Create(input.Category, input.Code, input.Name, input.SortOrder);
        if (await repository.ExistsAsync(entity.Category, entity.Code, ct))
        {
            throw new ReferenceEnumCodeConflictException(entity.Category, entity.Code);
        }

        await repository.AddAsync(entity, ct);
        var location = new Uri($"/api/reference-data/enums/{entity.Id}", UriKind.Relative);
        return (ReferenceEnumMapper.ToDto(entity), location);
    }

    public async Task<ReferenceEnumDto> UpdateAsync(
        Guid id,
        UpdateReferenceEnumInput input,
        CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new ReferenceEnumNotFoundException(id);

        entity.Update(input.Name, input.SortOrder);
        await repository.UpdateAsync(entity, ct);
        return ReferenceEnumMapper.ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new ReferenceEnumNotFoundException(id);

        if (entity.Category == ReferenceEnumCategories.GeoDivisionKind
            && await geoDivisionRepository.AnyByKindAsync(entity.Code, ct))
        {
            throw new ReferenceEnumInUseException(entity.Category, entity.Code);
        }

        await repository.RemoveAsync(entity, ct);
    }
}
