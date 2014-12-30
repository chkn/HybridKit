all:
	@echo "Usage currently is 'make nuget'"
	@false

nuget: HybridKit.nuspec
	nuget pack HybridKit.nuspec
