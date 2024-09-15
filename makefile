sample: test
	cd Haumohio.EventSourcing.Sample; func start

test:
	cd Tests.Haumohio.EventSourcing; dotnet test

watch:
	cd Tests.Haumohio.EventSourcing; dotnet watch test

pack:
	dotnet pack -o $(NUGET_LOCAL) Haumohio.EventSourcing/