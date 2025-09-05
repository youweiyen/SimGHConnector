using System;
using System.Collections.Generic;
using System.Diagnostics;
using Grasshopper.Kernel;
using RestSharp;
using Rhino.Geometry;
using SimScale.Sdk.Api;
using SimScale.Sdk.Client;
using SimScale.Sdk.Model;
using System.Threading;
using System.IO;

namespace SimGH
{
    public class GH_RunSimulation : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GH_RunSimulation class.
        /// </summary>
        public GH_RunSimulation()
          : base("RunSim", "Run",
              "Run and Download Simulation Results",
              "SimGH", "1_SimScale")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "P", "Project ID to access project", GH_ParamAccess.item);
            pManager.AddGenericParameter("Configuration", "C", "API client Configuration", GH_ParamAccess.item);
            pManager.AddTextParameter("APIKey", "K", "Your API Key", GH_ParamAccess.item);
            pManager.AddGenericParameter("SimulationApi", "API", "Simulation API", GH_ParamAccess.item);
            pManager.AddGenericParameter("SimulationID", "ID", "Simulation ID", GH_ParamAccess.item);

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
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = default;
            SimScale.Sdk.Client.Configuration config = default;
            string apiKey = default;
            SimulationsApi simulationApi = new SimulationsApi();
            Guid? simulationId = default;

            DA.GetData(0, ref projectId);
            DA.GetData(1, ref config);
            DA.GetData(2, ref apiKey);
            DA.GetData(3, ref simulationApi);
            DA.GetData(4, ref simulationId);


            var simulationRunApi = new SimulationRunsApi(config);

            var restClient = new RestClient();

            var API_KEY_HEADER = "X-API-KEY";
            var API_KEY = apiKey;

            // Estimate simulation
            var maxRuntime = 0.0;
            try
            {
                var estimationResult = simulationApi.EstimateSimulationSetup(projectId, simulationId);
                Console.WriteLine("Simulation estimation: " + estimationResult);

                if (estimationResult.Duration != null)
                {
                    maxRuntime = System.Xml.XmlConvert.ToTimeSpan(estimationResult.Duration.IntervalMax).TotalSeconds;
                    maxRuntime = Math.Max(3600, maxRuntime * 2);
                }
                else
                {
                    maxRuntime = 36000;
                    Console.WriteLine("Simulation estimated duration not available, assuming max runtime of {0} seconds", maxRuntime);
                }
            }
            catch (ApiException ae)
            {
                if (ae.ErrorCode == 422)
                {
                    maxRuntime = 36000;
                    Console.WriteLine("Simulation estimation not available, assuming max runtime of {0} seconds", maxRuntime);
                }
                else
                {
                    throw ae;
                }
            }

            // Create simulation run
            var run = new SimulationRun(name: "Run");
            run = simulationRunApi.CreateSimulationRun(projectId, simulationId, run);
            var runId = run.RunId;
            Console.WriteLine("runId: " + runId);

            // Read simulation run and update with the deserialized model
            run = simulationRunApi.GetSimulationRun(projectId, simulationId, runId);
            simulationRunApi.UpdateSimulationRun(projectId, simulationId, runId, run);

            // Start simulation run and wait until it's finished
            simulationRunApi.StartSimulationRun(projectId, simulationId, runId);
            run = simulationRunApi.GetSimulationRun(projectId, simulationId, runId);

            HashSet<Status> terminalStatuses = new HashSet<Status> { Status.FINISHED, Status.CANCELED, Status.FAILED };

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            int failedTries = 0;
            while (!terminalStatuses.Contains(run.Status ?? Status.READY))
            {
                if (stopWatch.Elapsed.TotalSeconds > maxRuntime)
                {
                    throw new TimeoutException();
                }
                Thread.Sleep(30000);
                run = simulationRunApi.GetSimulationRun(projectId, simulationId, runId) ??
                    (++failedTries > 5 ? throw new Exception("HTTP request failed too many times.") : run);
                Console.WriteLine("Simulation run status: " + run?.Status + " - " + run?.Progress);
            }

            // Get result metadata and download results (response is paginated)
            SimulationRunResults results = simulationRunApi.GetSimulationRunResults(
                projectId: projectId,
                simulationId: simulationId,
                runId: runId,
                page: 1,
                limit: 100,
                type: "SOLUTION_FIELD"
            );

            // Download solution field
            SimulationRunResultSolution solutionInfo = (SimulationRunResultSolution)results.Embedded[0];
            var solutionRequest = new RestRequest(solutionInfo.Download.Url, Method.GET);
            solutionRequest.AddHeader(API_KEY_HEADER, API_KEY);
            using (var writer = File.OpenWrite(@"solution.zip"))
            {
                solutionRequest.ResponseWriter = responseStream =>
                {
                    using (responseStream)
                    {
                        responseStream.CopyTo(writer);
                    }
                };
                restClient.DownloadData(solutionRequest);
            }
            using (var zip = System.IO.Compression.ZipFile.OpenRead(@"solution.zip"))
            {
                Console.WriteLine("Result ZIP file content:");
                foreach (var entry in zip.Entries)
                {
                    Console.WriteLine(entry);
                }
            }

            

            
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("FD99BB52-72F6-4DCE-A373-77C285579592"); }
        }
    }
}