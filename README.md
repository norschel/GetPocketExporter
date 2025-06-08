# GetPocketExporter

Export all GetPocket links (URL, title), including additional data (tags, time added, time updated), in Mozilla Bookmarks HTML format for import into the [linkding bookmark manager](https://linkding.link/).

Works on all platforms that support .NET 9.0.

## Requirements

- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Pocket API credentials (ConsumerKey and AccessToken) -> step-by-step guide [Getting Started With the Pocket Developer API
](https://www.jamesfmackenzie.com/getting-started-with-the-pocket-developer-api/)

## Configuration

1. Copy `src/appsettings.json.example` to `src/appsettings.json`.
2. Add your Pocket API `ConsumerKey` and `AccessToken` to `src/appsettings.json`:

   ```json
   {
     "Pocket": {
       "ConsumerKey": "<your_consumer_key>",
       "AccessToken": "<your_access_token>"
     }
   }
   ```

## Build

```sh
dotnet build src/GetPocketExporter.csproj
```

## Run

```sh
dotnet run --project src/GetPocketExporter.csproj
```

## Output

- All GetPocket items are printed to the console.
- A Mozilla Bookmarks HTML file is created in `src/export/` as `YYYYMMDD_bookmarks.html` for import into linkding.
- Raw Pocket API responses are exported as JSON files in `src/export/`(as additional backup).

## Known Issues

- AccessToken needs to be generated manually.
- Tokens are stored in `src/appsettings.json` and should not be committed to version control because they are stored in plaintext.
- GetPocket throws for some items a GatewayTimeExeption. In this case you need to finetune sort, count and offset in code and export step-by-step.
- Finetuning sort, count and offnet is not implemented yet as console params. You need to change it in the code.
