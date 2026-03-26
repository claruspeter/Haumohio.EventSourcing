sample: test
	cd Haumohio.EventSourcing.Sample; func start

test:
	cd Tests.Haumohio.EventSourcing; dotnet test -l "console;verbosity=normal"

watch:
	cd Tests.Haumohio.EventSourcing; dotnet watch test -- -l "console;verbosity=normal"

pack:
	dotnet pack -o $(NUGET_LOCAL) Haumohio.EventSourcing/

refresh_blob:
	azcopy copy "./__devwork_backup/*" "http://127.0.0.1:10000/devstoreaccount1/proj-devwork-638730020346304188" --recursive --from-to LocalBlob