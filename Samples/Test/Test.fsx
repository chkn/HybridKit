#r "../../HybridKit.Apps/bin/Debug/HybridKit.dll"
#r "../../HybridKit.Apps/bin/Debug/HybridKit.Apps.dll"
open HybridKit

type App  = NewApp
type Home = HtmlView<"index.html">

let home = Home()


App.Run(home)
