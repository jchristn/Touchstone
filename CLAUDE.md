# Development requirements

Follow the applicable documents in `~/Code/Agents/requirements`, especially CODE_STYLE.md, BACKEND_TEST_ARCHITECTURE.md, TELEMETRY_REQUIREMENTS.md, REPOSITORY_REQUIREMENTS.md, and VERSIONING.md. Record conflicts before implementation.

Use block namespaces with sorted using directives inside the namespace, explicit local types, one class or enum per file, `_PascalCase` private fields, public XML documentation, guarded inputs, nullable reference types, and cancellation-aware async operations with ConfigureAwait(false). Validate properties with backing fields where needed. Avoid tuples and generic exceptions. Keep console output in console hosts and Cli, never Core or shared test definitions. Dispose owned resources. Do not use em-dashes.

Keep shared scenario definitions runner-agnostic. Touchstone's own contract suites may reference adapter projects because those adapters are the project under test. Consumer shared suites should reference only Core and their application under test. Build and test both target frameworks before release. Version changes and publishing require explicit user authorization.
