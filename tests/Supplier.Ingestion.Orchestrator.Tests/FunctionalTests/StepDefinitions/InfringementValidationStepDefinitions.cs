using FluentAssertions;
using Reqnroll;
using Supplier.Ingestion.Orchestrator.Api.Validators;

namespace Supplier.Ingestion.Orchestrator.Tests.FunctionalTests.StepDefinitions;

[Binding]
public class InfringementValidationStepDefinitions
{
    private string _plate = string.Empty;
    private decimal _amount;
    private string _externalId = string.Empty;
    private bool _isValid;
    private string _errors = string.Empty;

    [Given(@"an infringement with plate ""(.*)"", amount (.*) and external id ""(.*)""")]
    public void GivenAnInfringementWithFields(string plate, decimal amount, string externalId)
    {
        _plate = plate;
        _amount = amount;
        _externalId = externalId;
    }

    [When(@"the infringement is validated")]
    public void WhenTheInfringementIsValidated()
    {
        (_isValid, _errors) = InfringementValidator.Validate(_plate, _amount, _externalId);
    }

    [Then(@"the validation result should be valid")]
    public void ThenTheValidationResultShouldBeValid()
    {
        _isValid.Should().BeTrue();
    }

    [Then(@"no validation errors should be returned")]
    public void ThenNoValidationErrorsShouldBeReturned()
    {
        _errors.Should().BeEmpty();
    }

    [Then(@"the validation result should be invalid")]
    public void ThenTheValidationResultShouldBeInvalid()
    {
        _isValid.Should().BeFalse();
    }

    [Then(@"the validation errors should contain ""(.*)""")]
    public void ThenTheValidationErrorsShouldContain(string expectedError)
    {
        _errors.Should().Contain(expectedError);
    }
}
