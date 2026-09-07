using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.DTOs.Order;
using ECommerce.Application.DTOs.Product;
using ECommerce.Application.Validators.Auth;
using ECommerce.Application.Validators.Order;
using ECommerce.Application.Validators.Product;
using FluentAssertions;

namespace ECommerce.UnitTests.Validators;

public class ValidatorTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly ProductCreateDtoValidator _productCreateValidator = new();
    private readonly OrderQueryParametersValidator _orderQueryValidator = new();

    [Fact]
    public void RegisterRequestValidator_WhenValid_PassesValidation()
    {
        // Arrange
        var request = new RegisterRequest(
            "Adel Emam",
            "adel.emam@ecommerce.com",
            "StrongPass123!",
            "StrongPass123!"
        );

        // Act
        var result = _registerValidator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("short", "Password must be at least 8 characters long.")]
    [InlineData("NoSpecial123", "Password must contain at least one special character.")]
    [InlineData("nodigitspecial!", "Password must contain at least one uppercase letter.")]
    [InlineData("NO_LOWER_CASE1!", "Password must contain at least one lowercase letter.")]
    public void RegisterRequestValidator_WhenPasswordInvalid_FailsWithCorrectMessage(string password, string expectedMessage)
    {
        // Arrange
        var request = new RegisterRequest(
            "Adel Emam",
            "adel.emam@ecommerce.com",
            password,
            password
        );

        // Act
        var result = _registerValidator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedMessage));
    }

    [Fact]
    public void RegisterRequestValidator_WhenPasswordsDoNotMatch_FailsValidation()
    {
        // Arrange
        var request = new RegisterRequest(
            "Adel Emam",
            "adel.emam@ecommerce.com",
            "StrongPass123!",
            "DifferentPass123!"
        );

        // Act
        var result = _registerValidator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Passwords do not match.");
    }

    [Fact]
    public void ProductCreateDtoValidator_WhenPriceIsNegative_FailsValidation()
    {
        // Arrange
        var request = new ProductCreateDto(
            "Test Product",
            "Description of test product",
            -10m,
            5,
            "SKU-12345",
            1
        );

        // Act
        var result = _productCreateValidator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Price");
    }

    [Fact]
    public void OrderQueryParametersValidator_WhenToDateIsBeforeFromDate_FailsValidation()
    {
        // Arrange
        var parameters = new OrderQueryParameters
        {
            FromDateUtc = DateTime.UtcNow,
            ToDateUtc = DateTime.UtcNow.AddDays(-1),
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var result = _orderQueryValidator.Validate(parameters);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ToDateUtc");
    }
}
