sample: test
	cd Haumohio.EventSourcing.Sample; func start

test:
	cd Tests.Haumohio.EventSourcing; dotnet test -l "console;verbosity=normal"

watch:
	cd Tests.Haumohio.EventSourcing; dotnet watch test -- -l "console;verbosity=normal"

pack:
	dotnet pack -o $(NUGET_LOCAL) Haumohio.EventSourcing/