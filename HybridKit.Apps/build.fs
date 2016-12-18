module HybridKit.Apps.Build

open HybridKit
open HybridKit.Apps.Cli
open HybridKit.Apps.Markup

open System
open System.IO
open System.Reflection

let defaultProjectPath (tk : TargetKind) =
    Path.Combine("proj", tk.ToString().ToLowerInvariant()), Path.Combine("..", "..")

let inflate nm fn destFile =
    using (new StreamReader(typeof<ViewTypeProvider>.Assembly.GetManifestResourceStream(nm)))
    <| Tree.fromMarkupReader ""
    |> Tree.toMarkup fn
    |> FS.writeTextFile destFile

let private interpolator scriptFile = function
| "ProjectGuid" -> Guid.NewGuid().ToString()
| "ProjectName" -> Path.GetFileNameWithoutExtension(scriptFile)
| "RootNamespace" -> "" // FIXME: Can't be the same as ^ because that will be module name
| "ScriptFile"  -> scriptFile
| "ScriptRelativePath" -> Path.GetDirectoryName(scriptFile)
// Android
| "AppClassName" -> Names.AndroidActivity
| other -> failwithf "Not handled: %s" other

let createAndroidProject projPath relScriptFile =
    let into = interpolator relScriptFile
    inflate "android.fsproj.xml" into (Path.Combine(projPath, Path.GetFileNameWithoutExtension(relScriptFile) + ".fsproj"))

    let propsDir = Path.Combine(projPath, "Properties")
    Directory.CreateDirectory(propsDir) |> ignore
    inflate "AndroidManifest.xml" into (Path.Combine(propsDir, "AndroidManifest.xml"))

    let drawable = Path.Combine(projPath, "Resources", "drawable")
    Directory.CreateDirectory(drawable) |> ignore
    FS.createFileFromResource "AndroidIcon.png" (Path.Combine(drawable, "Icon.png"))

let createProjectAndExit (target : TargetKind) =
    // Calculate paths
    let scriptFile = FSI.commandLineArgs.Force().[0]
    let relPathToProj, relPathToScript = defaultProjectPath target
    let relScriptFile = Path.Combine(relPathToScript, Path.GetFileName(scriptFile))

    // Create dir if necessary
    let projPath = Path.Combine(Path.GetDirectoryName(scriptFile), relPathToProj)
    Directory.CreateDirectory(projPath) |> ignore

    match target with
    | DebugServer -> failwith "Not supported yet"
    | Android -> createAndroidProject projPath relScriptFile

    Environment.Exit(0)
    Unchecked.defaultof<_>
