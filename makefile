pack:
	dotnet build --configuration Release
	dotnet pack --configuration Release --no-build -p:PackageVersion=0.1.0 --output /home/peter/Projects/HaumohioNuget  src/Haumohio.EventSourcing.fsproj

publish: pack
	dotnet nuget push dist/Haumohio.EventSourcing.0.1.0.nupkg --api-key $(NUGET_KEY) --source https://api.nuget.org/v3/index.json --interactive
	dotnet nuget push dist/Haumohio.EventSourcing.0.1.0.nupkg --api-key $(NUGET_KEY) --source https://api.nuget.org/v3/index.json --interactive