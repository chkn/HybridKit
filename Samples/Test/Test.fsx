#r "../../HybridKit.Apps/bin/Debug/HybridKit.dll"
#r "../../HybridKit.Apps/bin/Debug/HybridKit.Apps.dll"
open HybridKit

type App  = NewApp
type Home = HtmlView<__SOURCE_DIRECTORY__,"index.html">

let home = Home()
home.Headline <- "Todos"
home.Todo <- "Baz"

App.Run(home)