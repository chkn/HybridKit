all:
	@echo "Usage currently is 'make nupkg' or 'make publish'"
	@false

NUPKG  := Xam.Plugin.HybridKit.0.0.1-pre1.nupkg

nupkg: $(NUPKG)

$(NUPKG): HybridKit.nuspec
	nuget pack $<

publish: $(NUPKG)
	@echo "This will publish to the public NuGet gallery. Press any key to continue or Ctrl+C to cancel."; read
	nuget push $<

.PHONY: nupkg publish
