using Tiki.Shared.Querydsl;
using Tiki.Shared.Querydsl.Attributes;
using Tiki.Shared.Querydsl.Dto;
using Tiki.Shared.Querydsl.Enums;
using Xunit;

namespace Tiki.Shared.Tests.Querydsl;

/// <summary>
/// Proves Querydsl is entirely independent of EF Core: every test here runs against a
/// plain <c>List&lt;T&gt;.AsQueryable()</c> — no <c>DbContext</c>, no database — matching
/// the v1 release criterion that this independence is structural, not incidental.
/// </summary>
public class QuerydslExecutorTests
{
    private enum AccountStatus
    {
        Active,
        Suspended,
    }

    private sealed class Account
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required int Age { get; init; }
        public required bool IsActive { get; init; }
        public required AccountStatus Status { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }

        [QuerydslIgnore]
        public string InternalNotes { get; init; } = string.Empty;
    }

    private static IQueryable<Account> Accounts => new List<Account>
    {
        new()
        {
            Id = Guid.NewGuid(), Name = "Alice", Age = 30, IsActive = true,
            Status = AccountStatus.Active, CreatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
        },
        new()
        {
            Id = Guid.NewGuid(), Name = "Bob", Age = 45, IsActive = false,
            Status = AccountStatus.Suspended, CreatedAt = DateTimeOffset.Parse("2024-02-01T00:00:00Z"),
        },
        new()
        {
            Id = Guid.NewGuid(), Name = "Carol", Age = 22, IsActive = true,
            Status = AccountStatus.Active, CreatedAt = DateTimeOffset.Parse("2024-03-01T00:00:00Z"),
        },
    }.AsQueryable();

    [Fact]
    public void Filters_by_text_case_insensitively_by_default()
    {
        var query = new EntityQueryDto
        {
            Filters = [new FilterItem { PropertyName = "Name", Operation = FilterOperation.Equals, Value = "alice" }],
        };

        var result = QuerydslExecutor.Execute(Accounts, query);

        Assert.True(result.IsValid);
        Assert.Single(result.Value!.Items);
        Assert.Equal("Alice", result.Value!.Items[0].Name);
    }

    [Fact]
    public void Filters_numerically_with_greater_than()
    {
        var query = new EntityQueryDto
        {
            Filters = [new FilterItem { PropertyName = "Age", Operation = FilterOperation.GreaterThan, Value = "25" }],
        };

        var result = QuerydslExecutor.Execute(Accounts, query);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Value!.TotalCount);
    }

    [Fact]
    public void Combines_filters_with_and_by_default()
    {
        var query = new EntityQueryDto
        {
            Filters =
            [
                new FilterItem { PropertyName = "IsActive", Operation = FilterOperation.Equals, Value = "true" },
                new FilterItem { PropertyName = "Age", Operation = FilterOperation.LessThan, Value = "25" },
            ],
        };

        var result = QuerydslExecutor.Execute(Accounts, query);

        Assert.True(result.IsValid);
        Assert.Single(result.Value!.Items);
        Assert.Equal("Carol", result.Value!.Items[0].Name);
    }

    [Fact]
    public void Sorts_descending()
    {
        var query = new EntityQueryDto
        {
            Sorts = [new SortItem { PropertyName = "Age", Direction = SortDirection.Descending }],
        };

        var result = QuerydslExecutor.Execute(Accounts, query);

        Assert.True(result.IsValid);
        var names = result.Value!.Items.Select(a => a.Name).ToArray();
        Assert.Equal(new[] { "Bob", "Alice", "Carol" }, names);
    }

    [Fact]
    public void Paginates_and_reports_total_count_independent_of_page_size()
    {
        var query = new EntityQueryDto { Page = 1, PageSize = 2 };

        var result = QuerydslExecutor.Execute(Accounts, query);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.Equal(3, result.Value!.TotalCount);
        Assert.Equal(2, result.Value!.TotalPages);
    }

    [Fact]
    public void Caps_page_size_at_the_hard_ceiling_regardless_of_what_is_requested()
    {
        var query = new EntityQueryDto { Page = 1, PageSize = 100_000 };

        var result = QuerydslExecutor.Execute(Accounts, query);

        Assert.True(result.IsValid);
        Assert.Equal(QuerydslExecutor.MaxPageSize, result.Value!.PageSize);
    }

    [Fact]
    public void Filtering_on_a_QuerydslIgnore_property_is_a_validation_error_not_a_silent_no_op()
    {
        var query = new EntityQueryDto
        {
            Filters = [new FilterItem { PropertyName = "InternalNotes", Operation = FilterOperation.Equals, Value = "x" }],
        };

        var result = QuerydslExecutor.Execute(Accounts, query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "InternalNotes");
    }

    [Fact]
    public void An_unparseable_filter_value_is_collected_as_a_structured_error_not_thrown()
    {
        var query = new EntityQueryDto
        {
            Filters = [new FilterItem { PropertyName = "Age", Operation = FilterOperation.Equals, Value = "not-a-number" }],
        };

        var result = QuerydslExecutor.Execute(Accounts, query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Age");
    }

    [Fact]
    public void Multiple_bad_filters_are_all_collected_together_not_fail_on_first()
    {
        var query = new EntityQueryDto
        {
            Filters =
            [
                new FilterItem { PropertyName = "Age", Operation = FilterOperation.Equals, Value = "not-a-number" },
                new FilterItem { PropertyName = "DoesNotExist", Operation = FilterOperation.Equals, Value = "x" },
            ],
        };

        var result = QuerydslExecutor.Execute(Accounts, query);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Enum_filter_parses_by_member_name_case_insensitively()
    {
        var query = new EntityQueryDto
        {
            Filters = [new FilterItem { PropertyName = "Status", Operation = FilterOperation.Equals, Value = "suspended" }],
        };

        var result = QuerydslExecutor.Execute(Accounts, query);

        Assert.True(result.IsValid);
        Assert.Single(result.Value!.Items);
        Assert.Equal("Bob", result.Value!.Items[0].Name);
    }
}
