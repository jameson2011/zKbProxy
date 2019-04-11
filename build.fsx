#r @"packages/FAKE/tools/FakeLib.dll"
#r @"packages/FSharpLint.Fake/tools/FSharpLint.Core.dll"
#r @"packages/FSharpLint.Fake/tools/FSharpLint.Fake.dll"

open Fake
open FSharpLint.Fake

let buildDir = "./artifacts/"

// Targets
Target "ScrubArtifacts" (fun _ -> CleanDirs [ buildDir ])

Target "BuildApp" (fun _ -> 
                            !! "src/**/*.fsproj"
                            -- "src/**/*.Tests.fsproj"
                            |> MSBuildRelease buildDir "Build"
                            |> Log "AppBuild-Output: ")
Target "LintApp" (fun _ ->
                            !! "src/**/*.fsproj"
                            -- "src/**/*.Tests.fsproj"
                            |> Seq.iter (FSharpLint 
                                            (fun o -> { o with FailBuildIfAnyWarnings = false }))
                )
                
Target "Default" (fun _ -> trace "Done!" )


// Dependencies
"LintApp" 
==> "ScrubArtifacts" 
==> "BuildApp"
==> "Default"

RunTargetOrDefault "Default"