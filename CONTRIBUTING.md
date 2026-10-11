# Contributing

Use Windows and the .NET 10 SDK. Fork the repository, make a focused branch, and describe the behavior your change fixes or adds.

Run `dotnet build src/ThisWidget -c Release`, `dotnet run --project tests/ThisWidget.Tests`, and `dotnet run --project tests/ThisWidget.UiTests`. On an interactive Windows desktop, also run the UI suite with `--desktop` for Explorer attachment changes.

Add regression coverage for changed data/calendar rules or shared-view behavior. For UI changes, inspect the actual rendered app at default and minimum sizes and in both themes. Existing data must remain readable and migrations must preserve originals.

Keep the app local and free of analytics, network dependencies, secrets and committed journals. Generated screenshots must use demonstration data. CI builds and runs the standard tests on Windows; public releases include the runtime.

Use four-space C# indentation and two-space XAML/YAML indentation. Separate storage rules from WPF and Win32 integrations. Keep PRs focused and include the relevant test results and screenshots.
