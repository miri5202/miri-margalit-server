using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Workshops;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Service.Services;
using WorkshopHub.Tests.Helpers;

namespace WorkshopHub.Tests.Unit;

public class WorkshopServiceTests
{
    private readonly Mock<IWorkshopRepository> _workshops = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<ITagRepository> _tags = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly WorkshopService _service;

    private readonly Tag _beginners = new() { Id = 1, Name = "למתחילות" };
    private readonly Tag _evening = new() { Id = 2, Name = "ערב" };
    private readonly Tag _materials = new() { Id = 3, Name = "כולל חומרים" };

    public WorkshopServiceTests()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _categories.Setup(r => r.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // ה-repository המדומה מחזיר רק את התגיות שקיימות מתוך המזהים שהתבקשו
        var existingTags = new[] { _beginners, _evening, _materials };
        _tags
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                (IReadOnlyList<Tag>)existingTags.Where(tag => ids.Contains(tag.Id)).ToList());

        _service = new WorkshopService(
            _workshops.Object,
            _categories.Object,
            _tags.Object,
            _unitOfWork.Object,
            TestMapper.Instance,
            NullLogger<WorkshopService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_WithExistingCategoryAndTags_SavesWorkshopWithItsTags()
    {
        Workshop? added = null;
        _workshops
            .Setup(r => r.AddAsync(It.IsAny<Workshop>(), It.IsAny<CancellationToken>()))
            .Callback<Workshop, CancellationToken>((workshop, _) => added = workshop)
            .Returns(Task.CompletedTask);
        _workshops
            .Setup(r => r.GetDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => added);

        var result = await _service.CreateAsync(NewRequest(categoryId: 1, tagIds: new[] { 1, 2, 2 }), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal("קליגרפיה עברית", added!.Title);

        // מזהה כפול ברשימה לא יוצר שיוך כפול
        Assert.Equal(new[] { 1, 2 }, added.Tags.Select(t => t.Id).OrderBy(id => id));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownCategory_ReturnsValidationErrorAndSavesNothing()
    {
        var result = await _service.CreateAsync(NewRequest(categoryId: 99, tagIds: new[] { 1 }), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(ErrorCodes.RelatedEntityNotFound, result.Error.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownTag_ReturnsValidationErrorNamingTheMissingId()
    {
        var result = await _service.CreateAsync(NewRequest(categoryId: 1, tagIds: new[] { 1, 42 }), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("42", result.Error.Message);
        _workshops.Verify(r => r.AddAsync(It.IsAny<Workshop>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesTagsByAddingAndRemovingOnlyWhatChanged()
    {
        // לסדנה יש [למתחילות, ערב]; הבקשה מבקשת [ערב, כולל חומרים]
        var workshop = new Workshop
        {
            Id = 5,
            Title = "ישן",
            InstructorName = "אסתר וייס",
            CategoryId = 1,
            Tags = { _beginners, _evening }
        };
        _workshops.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(workshop);
        _workshops.Setup(r => r.GetDetailsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(workshop);

        var result = await _service.UpdateAsync(5, NewRequest(categoryId: 1, tagIds: new[] { 2, 3 }), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("קליגרפיה עברית", workshop.Title);
        Assert.Equal(new[] { 2, 3 }, workshop.Tags.Select(t => t.Id).OrderBy(id => id));

        // התגית שנשארה היא אותו אובייקט – היא לא הוסרה והוספה מחדש
        Assert.Contains(_evening, workshop.Tags);
    }

    [Fact]
    public async Task DeleteAsync_WhenWorkshopHasActiveRegistrations_ReturnsConflict()
    {
        var workshop = new Workshop { Id = 5, Title = "קליגרפיה עברית", CategoryId = 1 };
        _workshops.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(workshop);
        _workshops.Setup(r => r.HasActiveRegistrationsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.DeleteAsync(5, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(ErrorCodes.HasActiveRegistrations, result.Error.Code);
        _workshops.Verify(r => r.Remove(It.IsAny<Workshop>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenWorkshopDoesNotExist_ReturnsNotFound()
    {
        var result = await _service.GetByIdAsync(404, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    private static WorkshopRequest NewRequest(int categoryId, int[] tagIds) => new()
    {
        Title = "  קליגרפיה עברית ",
        Description = "אותיות הכתב המרובע",
        InstructorName = "אסתר וייס",
        CategoryId = categoryId,
        TagIds = tagIds.ToList()
    };
}
