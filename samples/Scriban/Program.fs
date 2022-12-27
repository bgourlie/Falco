module Falco.Scriban.Program

open System.IO
open System.Threading.Tasks
open Falco
open Falco.Routing
open Falco.HostBuilder
open Microsoft.Extensions.DependencyInjection
open Scriban

// ------------
// View Engine
// ------------
type IViewEngine =
    abstract member RenderAsync : view : string * model : 'a -> ValueTask<string>

type ScribanViewEngine (views : Map<string, Template>) =
    interface IViewEngine with
        member _.RenderAsync(view : string, model : 'a) =
            match Map.tryFind view views with
            | Some template -> template.RenderAsync(model)
            | None -> failwithf "View '%s' was not found" view

// ------------
// Pages
// ------------
module Pages =
    let homepage : HttpHandler =
        Services.inject<IViewEngine> (fun viewEngine ->
            let queryMap (q: QueryCollectionReader) =
                {| Name = q.Get "name" |}

            let next model : HttpHandler = fun ctx ->
                task {
                    let! html = viewEngine.RenderAsync("Home", model)
                    return Response.ofHtmlString html ctx
                }

            Request.mapQuery queryMap next)

// ------------
// Services
// ------------
let scribanService scribanTemplates (svc : IServiceCollection) =
    svc.AddScoped<IViewEngine, ScribanViewEngine>(fun _ ->
        new ScribanViewEngine(scribanTemplates))

[<EntryPoint>]
let main args =
    let scribanTemplates =
        let root = Directory.GetCurrentDirectory()
        let viewsDirectory = Path.Combine(root, "Views")

        Directory.EnumerateFiles(viewsDirectory)
        |> Seq.map (fun file ->
            let viewName = Path.GetFileNameWithoutExtension(file)
            let viewContent = File.ReadAllText(file)
            let view = Template.Parse(viewContent)
            viewName, view)
        |> Map.ofSeq


    webHost [||] {
        use_https

        add_service (scribanService scribanTemplates)

        endpoints [
            get "/" Pages.homepage
        ]
    }

    0 // Exit code