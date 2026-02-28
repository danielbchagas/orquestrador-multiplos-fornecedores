Feature: Supplier B State Machine
    As the orchestrator system
    I want to process infringement events from Supplier B
    So that valid infringements are unified and invalid ones are sent to the DLQ

    Scenario: Valid infringement from Supplier B is processed successfully
        Given a valid infringement event from Supplier B with plate "XYZ9876" and amount 200.00
        When the event is published to the bus
        Then the saga should be finalized
        And a unified infringement processed event should be produced
        And no validation failed event should be produced

    Scenario: Invalid infringement from Supplier B with negative amount is rejected
        Given an invalid infringement event from Supplier B with plate "XYZ9876" and amount -5.00
        When the event is published to the bus
        Then the saga should be finalized
        And a validation failed event should be produced
        And no unified infringement processed event should be produced

    Scenario: Invalid infringement from Supplier B with empty plate is rejected
        Given an invalid infringement event from Supplier B with plate "" and amount 50.00
        When the event is published to the bus
        Then the saga should be finalized
        And a validation failed event should be produced
        And no unified infringement processed event should be produced
