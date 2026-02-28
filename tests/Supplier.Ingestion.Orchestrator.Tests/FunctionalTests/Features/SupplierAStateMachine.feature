Feature: Supplier A State Machine
    As the orchestrator system
    I want to process infringement events from Supplier A
    So that valid infringements are unified and invalid ones are sent to the DLQ

    Scenario: Valid infringement from Supplier A is processed successfully
        Given a valid infringement event from Supplier A with plate "ABC1234" and amount 150.00
        When the event is published to the bus
        Then the saga should be finalized
        And a unified infringement processed event should be produced
        And no validation failed event should be produced

    Scenario: Invalid infringement from Supplier A with negative amount is rejected
        Given an invalid infringement event from Supplier A with plate "ABC1234" and amount -10.00
        When the event is published to the bus
        Then the saga should be finalized
        And a validation failed event should be produced
        And no unified infringement processed event should be produced

    Scenario: Invalid infringement from Supplier A with empty plate is rejected
        Given an invalid infringement event from Supplier A with plate "" and amount 100.00
        When the event is published to the bus
        Then the saga should be finalized
        And a validation failed event should be produced
        And no unified infringement processed event should be produced
