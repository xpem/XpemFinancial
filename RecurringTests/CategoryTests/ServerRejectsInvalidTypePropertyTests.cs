// Feature: category-type-classification, Property 8: Server rejects invalid Type values
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FinancialService.Model.Req;
using FinancialService.Repo;
using FinancialService.Service;
using NSubstitute;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property-based tests for server-side rejection of invalid Type values.
/// For any integer not in {0, 1, 2}, UpsertAsync shall throw ArgumentException.
/// **Validates: Requirements 8.3**
/// </summary>
[Trait("Feature", "category-type-classification")]
[Trait("Property", "8")]
public class ServerRejectsInvalidTypePropertyTests
{
    /// <summary>
    /// Generates an integer that is NOT in the valid set {0, 1, 2}.
    /// </summary>
    private static Gen<int> InvalidTypeValue()
    {
        return Gen.OneOf(
            Gen.Choose(int.MinValue, -1),
            Gen.Choose(3, int.MaxValue));
    }

    /// <summary>
    /// Property 8: For any integer value not in {0, 1, 2}, when included as the Type field
    /// in a category upsert request, the server shall reject with an ArgumentException.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertAsync_WithInvalidType_ThrowsArgumentException()
    {
        return Prop.ForAll(
            InvalidTypeValue().ToArbitrary(),
            invalidType =>
            {
                // Arrange
                var repo = Substitute.For<ITransactionCategoryRepo>();
                var service = new TransactionCategoryService(repo);

                var req = new TransactionCategoryReq
                {
                    Name = "Test Category",
                    Type = invalidType
                };

                // Act & Assert
                var ex = Assert.ThrowsAsync<ArgumentException>(
                    () => service.UpsertAsync(req, uid: 1)).Result;

                return (ex.Message == "Invalid category type value.")
                    .Label($"Type={invalidType} should be rejected with ArgumentException, got message: '{ex.Message}'");
            });
    }
}
