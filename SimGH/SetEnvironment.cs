using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using static Rhino.Render.Dithering;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Diagnostics;
using Newtonsoft.Json;
using RestSharp;
using SimScale.Sdk.Api;
using SimScale.Sdk.Client;
using SimScale.Sdk.Model;
using System.IO;
using System.Threading;
using static System.Net.WebRequestMethods;


namespace SimGH
{
    public class SetEnvironment : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public SetEnvironment()
          : base("SetEnv", "SE",
            "Set up connection environment for SimScale",
            "SimGH", "Set")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectName", "N", "Project Name", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Project Description", GH_ParamAccess.item);
            pManager.AddTextParameter("APIKey", "K", "API Key", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectName = default;
            string projectDescription = default;
            DA.GetData(0, ref projectName);
            DA.GetData(0, ref projectDescription);
            // API client configuration
            var API_KEY_HEADER = "X-API-KEY";
            //var API_KEY = Environment.GetEnvironmentVariable("SIMSCALE_API_KEY");
            //var API_URL = Environment.GetEnvironmentVariable("SIMSCALE_API_URL");
            var API_URL = "https://api.simscale.com";
            var API_KEY = "ea4c2f66-e56d-489d-8a74-fc7b6658fe7c";

            Configuration config = new Configuration();
            config.BasePath = API_URL + "/v0";
            config.ApiKey.Add(API_KEY_HEADER, API_KEY);

            // API clients
            var restClient = new RestClient();
            var projectApi = new ProjectsApi(config);
            var storageApi = new StorageApi(config);
            var geometryImportApi = new GeometryImportsApi(config);
            var geometryApi = new GeometriesApi(config);
            var meshOperationApi = new MeshOperationsApi(config);
            var simulationApi = new SimulationsApi(config);
            var simulationRunApi = new SimulationRunsApi(config);
            var tableImportApi = new TableImportsApi(config);
            var reportsApi = new ReportsApi(config);
            var materialsApi = new MaterialsApi(config);

            HashSet<SimScale.Sdk.Model.Status> terminalStatuses = new HashSet<SimScale.Sdk.Model.Status> { SimScale.Sdk.Model.Status.FINISHED, SimScale.Sdk.Model.Status.CANCELED, SimScale.Sdk.Model.Status.FAILED };
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Restart();
            var failedTries = 0;

            // Create project
            var project = new Project(
                name: projectName,
                description: projectDescription,
                measurementSystem: Project.MeasurementSystemEnum.SI
            );
            project = projectApi.CreateProject(project);
            var projectId = project.ProjectId;
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "projectId: " + projectId);

            // Upload CAD
            var storage = storageApi.CreateStorage();
            var storageId = storage.StorageId;
            var uploadRequest = new RestRequest(storage.Url, Method.PUT);
            uploadRequest.AddParameter("application/octet-stream", System.IO.File.ReadAllBytes(@"../fixtures/pipe_junction_model_tutorial.x_t"), ParameterType.RequestBody);
            restClient.Execute(uploadRequest);
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "storageId: " + storageId);

            // Import CAD
            var geometryImportRequest = new GeometryImportRequest(
                name: "Pipe junction",
                location: new GeometryImportRequestLocation(storageId),
                format: GeometryImportRequest.FormatEnum.PARASOLID,
                inputUnit: GeometryUnit.M,
                options: new GeometryImportRequestOptions(facetSplit: false, sewing: false, improve: true, optimizeForLBMSolver: false)
            );
            var geometryImport = geometryImportApi.ImportGeometry(projectId, geometryImportRequest);
            var geometryImportId = geometryImport.GeometryImportId.Value;
            stopWatch.Restart();
            failedTries = 0;
            while (!terminalStatuses.Contains(geometryImport.Status))
            {
                // adjust timeout for larger geometries
                if (stopWatch.Elapsed.TotalSeconds > 900)
                {
                    throw new TimeoutException();
                }
                Thread.Sleep(10000);
                geometryImport = geometryImportApi.GetGeometryImport(projectId, geometryImportId) ??
                    (++failedTries > 5 ? throw new Exception("HTTP request failed too many times.") : geometryImport);
                Console.WriteLine("Geometry import status: " + geometryImport?.Status);
            }
            var geometryId = geometryImport.GeometryId.Value;
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "geometryId: " + geometryId);

            // Read geometry information and update with the deserialized model
            var geometry = geometryApi.GetGeometry(projectId, geometryId);
            geometryApi.UpdateGeometry(projectId, geometryId, geometry);
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("c001932d-7118-42c2-9f05-d5442a97922b");
    }
}