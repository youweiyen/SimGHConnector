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
using Rhino.Commands;
using System.Threading.Tasks;
using Grasshopper.Kernel.Types.Transforms;
using Rhino;

namespace SimGH
{
    public class GH_GenerateMeshAsync : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GH_CreateMesh class.
        /// </summary>
        public GH_GenerateMeshAsync()
          : base("GenerateMesh", "M",
              "GenerateMesh",
              "SimGH", "1_SimScale")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Projectinfo", "I", "SimScale Project Info", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Fineness", "F", "Mesh Fineness, default set to 5", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Generate", "T", "Generate Mesh in SimScale", GH_ParamAccess.item);
            pManager[1].Optional = true;

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Log", "L", "Result Messages", GH_ParamAccess.list);
        }

        SimProjectInfo simProjectInfo = new SimProjectInfo();
        private bool _shouldExpire = false;
        private List<string> _message = new List<string>();

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (_shouldExpire)
            {
                //second call
                DA.SetDataList(0, _message);
                _shouldExpire = false;
            }

            int inFineness = 5;
            bool generate = false;
            if (!DA.GetData(0, ref simProjectInfo)) return;
            DA.GetData(1, ref inFineness);
            DA.GetData(2, ref generate);

            if (generate)
            { 
                var projectId = simProjectInfo.ProjectId;
                Configuration config = simProjectInfo.Configuration;
                var geometryId = simProjectInfo.GeometryId;
                var simulationId = simProjectInfo.SimulationId;
                var simulationApi = simProjectInfo.SimulationApi;
                var simulationSpec = simProjectInfo.SimulationSpec;
                var meshOperationApi = new MeshOperationsApi(config);

                CreateMeshAsync( meshOperationApi, projectId, geometryId, simulationId, simulationSpec, simulationApi, inFineness);
            }
            //DA.SetDataList(0, _message);


        }

        private void CreateMeshAsync(MeshOperationsApi meshOperationApi, string projectId, Guid? geometryId, Guid? simulationId, SimulationSpec simulationSpec, SimulationsApi simulationApi, int fineness)
        {
            Task.Run(() =>
            {

                var meshOperation = meshOperationApi.CreateMeshOperation(projectId, new MeshOperation(
                    name: "APIMesh",
                    geometryId: geometryId,
                    model: new SimmetrixMeshingSolid(sizing: new AutomaticMeshSizingSimmetrix(fineness: (decimal)fineness))
                ));
                var meshOperationId = meshOperation.MeshOperationId;
                //AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "meshOperationId: " + meshOperationId);

                // Check mesh operation setup
                var meshCheck = meshOperationApi.CheckMeshOperationSetup(projectId, meshOperationId, simulationId);
                var warnings = meshCheck.Entries.Where(e => e.Severity == LogSeverity.WARNING).ToList();

                //AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Mesh operation setup check warnings:");
                //warnings.ForEach(i => AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{i}"));
                
                Message = "Checking Warnings...";

                var errors = meshCheck.Entries.Where(e => e.Severity == LogSeverity.ERROR).ToList();
                if (errors.Any())
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Mesh operation setup check errors:");
                    errors.ForEach(i => AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"{i}"));
                    throw new Exception("Simulation check failed");
                }

                // Estimate mesh operation
                var maxRuntime = 0.0;
                try
                {
                    var estimationResult = meshOperationApi.EstimateMeshOperation(projectId, meshOperationId);
                    //AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Mesh operation estimation: " + estimationResult);

                    if (estimationResult.Duration != null)
                    {
                        maxRuntime = System.Xml.XmlConvert.ToTimeSpan(estimationResult.Duration.IntervalMax).TotalSeconds;
                        maxRuntime = Math.Max(3600, maxRuntime * 2);
                    }
                    else
                    {
                        maxRuntime = 36000;
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Mesh operation estimated duration not available, assuming max runtime of {maxRuntime} seconds");
                    }
                }
                catch (ApiException ae)
                {
                    if (ae.ErrorCode == 422)
                    {
                        maxRuntime = 36000;
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Mesh operation estimation not available, assuming max runtime of {maxRuntime} seconds");
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

                Message = "Meshing...";
                while (!terminalStatuses.Contains(meshOperation.Status ?? Status.READY))
                {
                    if (stopWatch.Elapsed.TotalSeconds > maxRuntime)
                    {
                        throw new TimeoutException();
                    }
                    Thread.Sleep(30000);
                    meshOperation = meshOperationApi.GetMeshOperation(projectId, meshOperationId) ??
                        (++failedTries > 5 ? throw new Exception("HTTP request failed too many times.") : meshOperation);
                    //AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Mesh operation status: " + meshOperation?.Status + " - " + meshOperation?.Progress);
                    string progress = String.Format("{0: 0}", (meshOperation?.Progress)*100);
                    Message = $"{progress}%";

                }

                _message.Add("final mesh operation: " + meshOperation);

                // Read simulation and update with the finished mesh
                simulationSpec = simulationApi.GetSimulation(projectId, simulationId);
                simulationSpec.MeshId = meshOperation.MeshId;
                simulationApi.UpdateSimulation(projectId, simulationId, simulationSpec);
                
                Message = "Checking...";
                // Check simulation
                var checkResult = simulationApi.CheckSimulationSetup(projectId, simulationId);
                warnings = checkResult.Entries.Where(e => e.Severity == LogSeverity.WARNING).ToList();
                _message.Add("Simulation check warnings:");
                var checkMessages = warnings.Select(i => ($"{i}")).ToList();
                _message.AddRange(checkMessages);
                errors = checkResult.Entries.Where(e => e.Severity == LogSeverity.ERROR).ToList();
                if (errors.Any())
                {
                    _message.Add("Simulation check errors:");
                    var errorMessages = errors.Select(i => ($"{i}")).ToList();
                    _message.AddRange(errorMessages);
                    throw new Exception("Simulation check failed");
                }
                Message = "Completed";

                _shouldExpire = true;
                RhinoApp.InvokeOnUiThread((Action)delegate { ExpireSolution(true); });

            });

            
        }
        protected override void ExpireDownStreamObjects()
        {
            if (_shouldExpire)
            {
                base.ExpireDownStreamObjects();

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
            get { return new Guid("1810e641-c1ed-42c9-975c-039be4e176d7"); }
        }
    }
}