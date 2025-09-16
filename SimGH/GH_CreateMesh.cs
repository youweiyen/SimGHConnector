using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using SimScale.Sdk.Client;
using SimScale.Sdk.Model;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Grasshopper.Rhinoceros.Params;
using SimScale.Sdk.Api;

namespace SimGH
{
    public class GH_CreateMesh : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GH_CreateMesh class.
        /// </summary>
        public GH_CreateMesh()
          : base("GenerateMesh", "Nickname",
              "Description",
              "Category", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }
        SimProjectInfo simProjectInfo = new SimProjectInfo();

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            bool generate = false;
            DA.GetData(0, ref simProjectInfo);
            DA.GetData(1, ref generate);

            var projectId = simProjectInfo.ProjectId;
            Configuration config = simProjectInfo.Configuration;
            var geometryId = simProjectInfo.GeometryId;
            var simulationId = simProjectInfo.SimulationId;
            var simulationApi = simProjectInfo.SimulationApi;
            var simulationSpec = simProjectInfo.SimulationSpec;

            var meshOperationApi = new MeshOperationsApi(config);


            if (generate)
            {
                var meshOperation = meshOperationApi.CreateMeshOperation(projectId, new MeshOperation(
                    name: "APIMesh",
                    geometryId: geometryId,
                    model: new SimmetrixMeshingSolid()
                ));
                var meshOperationId = meshOperation.MeshOperationId;
                Console.WriteLine("meshOperationId: " + meshOperationId);

                // Check mesh operation setup
                var meshCheck = meshOperationApi.CheckMeshOperationSetup(projectId, meshOperationId, simulationId);
                var warnings = meshCheck.Entries.Where(e => e.Severity == LogSeverity.WARNING).ToList();

                Console.WriteLine("Mesh operation setup check warnings:");
                warnings.ForEach(i => Console.WriteLine("{0}", i));
                
                var errors = meshCheck.Entries.Where(e => e.Severity == LogSeverity.ERROR).ToList();
                if (errors.Any())
                {
                    Console.WriteLine("Mesh operation setup check errors:");
                    errors.ForEach(i => Console.WriteLine("{0}", i));
                    throw new Exception("Simulation check failed");
                }

                // Estimate mesh operation
                var maxRuntime = 0.0;
                try
                {
                    var estimationResult = meshOperationApi.EstimateMeshOperation(projectId, meshOperationId);
                    Console.WriteLine("Mesh operation estimation: " + estimationResult);

                    if (estimationResult.Duration != null)
                    {
                        maxRuntime = System.Xml.XmlConvert.ToTimeSpan(estimationResult.Duration.IntervalMax).TotalSeconds;
                        maxRuntime = Math.Max(3600, maxRuntime * 2);
                    }
                    else
                    {
                        maxRuntime = 36000;
                        Console.WriteLine("Mesh operation estimated duration not available, assuming max runtime of {0} seconds", maxRuntime);
                    }
                }
                catch (ApiException ae)
                {
                    if (ae.ErrorCode == 422)
                    {
                        maxRuntime = 36000;
                        Console.WriteLine("Mesh operation estimation not available, assuming max runtime of {0} seconds", maxRuntime);
                    }
                    else
                    {
                        throw ae;
                    }
                }

                // Start mesh operation and wait until it's finished
                meshOperationApi.StartMeshOperation(projectId, meshOperationId, simulationId);
                meshOperation = meshOperationApi.GetMeshOperation(projectId, meshOperationId);

                Stopwatch stopWatch = Stopwatch.StartNew();
                HashSet<Status> terminalStatuses = new HashSet<Status> { Status.FINISHED, Status.CANCELED, Status.FAILED };


                stopWatch.Restart();
                int failedTries = 0;
                while (!terminalStatuses.Contains(meshOperation.Status ?? Status.READY))
                {
                    if (stopWatch.Elapsed.TotalSeconds > maxRuntime)
                    {
                        throw new TimeoutException();
                    }
                    Thread.Sleep(30000);
                    meshOperation = meshOperationApi.GetMeshOperation(projectId, meshOperationId) ??
                        (++failedTries > 5 ? throw new Exception("HTTP request failed too many times.") : meshOperation);
                    Console.WriteLine("Mesh operation status: " + meshOperation?.Status + " - " + meshOperation?.Progress);
                }

                Console.WriteLine("final mesh operation: " + meshOperation);

                // Read simulation and update with the finished mesh
                simulationSpec = simulationApi.GetSimulation(projectId, simulationId);
                simulationSpec.MeshId = meshOperation.MeshId;
                simulationApi.UpdateSimulation(projectId, simulationId, simulationSpec);

                // Check simulation
                var checkResult = simulationApi.CheckSimulationSetup(projectId, simulationId);
                warnings = checkResult.Entries.Where(e => e.Severity == LogSeverity.WARNING).ToList();
                Console.WriteLine("Simulation check warnings:");
                warnings.ForEach(i => Console.WriteLine("{0}", i));
                errors = checkResult.Entries.Where(e => e.Severity == LogSeverity.ERROR).ToList();
                if (errors.Any())
                {
                    Console.WriteLine("Simulation check errors:");
                    errors.ForEach(i => Console.WriteLine("{0}", i));
                    throw new Exception("Simulation check failed");
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
            get { return new Guid("2B03EC98-8C45-4FF6-885A-55C39A5DAAF6"); }
        }
    }
}