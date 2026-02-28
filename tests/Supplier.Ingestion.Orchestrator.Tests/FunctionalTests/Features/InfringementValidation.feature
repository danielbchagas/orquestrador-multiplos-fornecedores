Feature: Infringement Validation
    As the orchestrator system
    I want to validate infringement data before processing
    So that only correct data flows through the pipeline

    Scenario: All fields are valid
        Given an infringement with plate "ABC1234", amount 100.00 and external id "EXT-001"
        When the infringement is validated
        Then the validation result should be valid
        And no validation errors should be returned

    Scenario: Empty plate is invalid
        Given an infringement with plate "", amount 100.00 and external id "EXT-002"
        When the infringement is validated
        Then the validation result should be invalid
        And the validation errors should contain "Placa obrigatória"

    Scenario: Negative amount is invalid
        Given an infringement with plate "ABC1234", amount -50.00 and external id "EXT-003"
        When the infringement is validated
        Then the validation result should be invalid
        And the validation errors should contain "Valor inválido"

    Scenario: Empty external id is invalid
        Given an infringement with plate "ABC1234", amount 100.00 and external id ""
        When the infringement is validated
        Then the validation result should be invalid
        And the validation errors should contain "ID de origem não informado"

    Scenario: Multiple validation errors
        Given an infringement with plate "", amount -10.00 and external id ""
        When the infringement is validated
        Then the validation result should be invalid
        And the validation errors should contain "Placa obrigatória"
        And the validation errors should contain "Valor inválido"
        And the validation errors should contain "ID de origem não informado"
