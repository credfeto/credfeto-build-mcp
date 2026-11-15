namespace Credfeto.Build.Dotnet.Server.Tests.Packages;

// Shapes captured from dotnet package search --exact-match --format json on SDK 11.0.100
internal static class PackageSearchFixtures
{
    public const string FOUND = """
        {
          "version": 2,
          "problems": [],
          "searchResult": [
            {
              "sourceName": "Private",
              "packages": [
                { "id": "Example.Package", "version": "1.2.0" },
                { "id": "Example.Package", "version": "1.10.0-beta.1" }
              ]
            },
            {
              "sourceName": "Public",
              "packages": [
                { "id": "Example.Package", "version": "1.0.0" },
                { "id": "Example.Package", "version": "1.1.0" },
                { "id": "Example.Package", "version": "1.2.0-rc.1" },
                { "id": "Example.Package", "version": "1.2.0" },
                { "id": "Example.Package", "version": "1.3.0" },
                { "id": "Other.Package", "version": "9.0.0" }
              ]
            },
            {
              "sourceName": "Empty",
              "packages": []
            }
          ]
        }
        """;

    public const string NOT_FOUND = """
        {
          "version": 2,
          "problems": [],
          "searchResult": [
            { "sourceName": "Public", "packages": [] },
            {
              "sourceName": "Broken",
              "problems": [
                { "text": "Unable to load the service index for source https://127.0.0.1:9/v3/index.json.", "problemType": "Error" }
              ],
              "packages": []
            }
          ]
        }
        """;
}
