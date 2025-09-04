using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types.Transforms;
using Rhino.Geometry;
using SimScale.Sdk.Api;
using SimScale.Sdk.Model;
using SimScale.Sdk.Client;
using System.Linq;
using System.Configuration;
using Point = SimScale.Sdk.Model.Point;
using System.Diagnostics;
using System.Threading;
using System.Drawing;

namespace SimGH
{
    public class GH_CreateSimulation : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GH_CreateSimulation class.
        /// </summary>
        public GH_CreateSimulation()
          : base("CreateSim", "CS",
              "Create Simulation Model",
              "SimGH", "Set")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectName", "N", "Project Name", GH_ParamAccess.item);
            pManager.AddTextParameter("ProjectID", "P", "ProjectID", GH_ParamAccess.item);
            pManager.AddTextParameter("GeometryID", "G", "GeometryID", GH_ParamAccess.item);
            pManager.AddGenericParameter("Configuration", "C", "API Client Configuration", GH_ParamAccess.item);

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
            string projectName = default;
            string projectId = default;
            string geometryIdText = default;
            SimScale.Sdk.Client.Configuration config = default;

            DA.GetData(0, ref projectName);
            DA.GetData(1, ref projectId);
            DA.GetData(2, ref geometryIdText);
            DA.GetData(3, ref config);

            Guid geometryId = new Guid(geometryIdText);

            var geometryApi = new GeometriesApi(config);
            var meshOperationApi = new MeshOperationsApi(config);
            var simulationApi = new SimulationsApi(config);
            var materialsApi = new MaterialsApi(config);

            //var simulationRunApi = new SimulationRunsApi(config);
            //var tableImportApi = new TableImportsApi(config);
            //var reportsApi = new ReportsApi(config);

            // Read geometry information and update with the deserialized model
            var geometry = geometryApi.GetGeometry(projectId, geometryId);
            geometryApi.UpdateGeometry(projectId, geometryId, geometry);




            // Get geometry mappings

            var bc1Entity = geometryApi.GetGeometryMappings(
                projectId: projectId,
                geometryId: geometryId,
                _class: "face"
            ).Embedded;
            var bc2entity = geometryApi.GetGeometryMappings(
                projectId: projectId,
                geometryId: geometryId,
                _class: "region"
            ).Embedded;
            var bc2Entity = getSingleEntityName(
                geometryApi,
                projectId,
                geometryId,
                values: new List<string> { "b10" }
            );
            var entities = geometryApi.GetGeometryMappings(
                projectId: projectId,
                geometryId: geometryId,
                attributes: new List<string> { "SDL/TYSA_COLOUR" },
                values: new List<string> {"[0 0 1]" }
            ).Embedded;
            var materialEntity = getSingleEntityColor(
                geometryApi,
                projectId,
                geometryId,
                values: new List<string> { "[0 0 1]" }
            );


            // Initialize simulation model
            var simulationModel = new HeatTransfer (
                timeDependency: default,
                nonLinearAnalysis: false,
                connectionGroups: new List<Contact>(),
                elementTechnology: new SolidElementTechnology(new ElementTechnology(new AutomaticElementDefinitionMethod())),
                model: new SolidModel(),
                materials: new List<SolidMaterial>(),
                initialConditions: new SolidInitialConditions(),
                boundaryConditions: 
                new List<OneOfHeatTransferBoundaryConditions>()
                {
                    //new SurfaceHeatFluxBC(
                    //    name: "HeatPlate",
                    //    heatfluxValue: new DimensionalFunctionHeatFlux(),
                    //    topologicalReference: new TopologicalReference(
                    //        entities: new List<string>() { bc1Entity }
                    //    )
                    //),
                    //new ConvectiveHeatFluxBC(
                    //    name: "TopSurface"),
                    new FixedTemperatureValueBC(
                        name: "HeatTemperature",
                        temperatureValue: new DimensionalFunctionTemperature(),
                        topologicalReference: new TopologicalReference(
                            entities: new List<string>() { bc1Entity[0].Name })
                        ),
                    new FixedTemperatureValueBC(
                        name: "SurfaceTemperature",
                        temperatureValue: new DimensionalFunctionTemperature(),
                        topologicalReference: new TopologicalReference(
                            entities: new List<string>() { bc2Entity})
                        ),
                },
                numerics: new SolidNumerics(
                    solver: new MUMPSSolver("MUMPS", new AdvancedMUMPSSettings())
                    ),
                
                simulationControl: new SolidSimulationControl(
                    processors: new ComputingCore(),
                    maxRunTime: new DimensionalTime(3600, DimensionalTime.UnitEnum.S)),
                resultControl: new SolidResultControl(),
                meshOrder: default
            );
            var simulationSpec = new SimulationSpec(name: projectName, geometryId: geometryId, model: simulationModel);

            // Create simulation first to use for physics based meshing
            var simulationId = simulationApi.CreateSimulation(projectId, simulationSpec).SimulationId;
            Console.WriteLine("simulationId: " + simulationId);

            // Add a material to the simulation
            var materialGroups = materialsApi.GetMaterialGroups().Embedded;
            var defaultMaterialGroup = materialGroups.FirstOrDefault(group => group.GroupType == MaterialGroupType.SIMSCALEDEFAULT);
            if (defaultMaterialGroup == null)
            {
                throw new Exception("Couldn't find default material group in " + materialGroups);
            }

            var defaultMaterials = materialsApi.GetMaterials(defaultMaterialGroup.MaterialGroupId).Embedded;
            var Wood1 = defaultMaterials.FirstOrDefault(material => material.Name == "Wood");
            if (Wood1 == null)
            {
                throw new Exception("Couldn't find default Air material in " + defaultMaterials);
            }

            var materialData = materialsApi.GetMaterialData(defaultMaterialGroup.MaterialGroupId, Wood1.Id);
            var materialUpdateRequest = new MaterialUpdateRequest(
                operations: new List<MaterialUpdateOperation> {
                new MaterialUpdateOperation(
                    path: "/materials/fluids",
                    materialData: materialData,
                    reference: new MaterialUpdateOperationReference(
                        materialGroupId: defaultMaterialGroup.MaterialGroupId,
                        materialId: Wood1.Id
                    )
                )
                }
            );
            var materialUpdateResponse = simulationApi.UpdateSimulationMaterials(projectId, simulationId, materialUpdateRequest);

            // Add assignments to the new material
            simulationSpec = simulationApi.GetSimulation(projectId, simulationId);
            var materials = ((ConvectiveHeatTransfer)simulationSpec.Model).Materials.Fluids;
            ((IncompressibleMaterial)materials.First()).TopologicalReference = new TopologicalReference(entities: new List<string>() { materialEntity });
            simulationApi.UpdateSimulation(projectId, simulationId, simulationSpec);

            // Create mesh operation
            var meshOperation = meshOperationApi.CreateMeshOperation(projectId, new MeshOperation(
                name: "WoodMesh",
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
        }


        public static string getSingleEntityName(GeometriesApi geometryApi, string projectId, Guid geometryId,
                                       List<string> values = null)
        {
            var entities = geometryApi.GetGeometryMappings(
                projectId: projectId,
                geometryId: geometryId,
                attributes: new List<string> { "ATTRIB_XPARASOLID_NAME" },
                values: values
            ).Embedded;
            if (entities.Count == 1)
            {
                return entities[0].Name;
            }
            else
            {
                throw new Exception("Unexpected number of entities returned");
            }
        }
        public static string getSingleEntityColor(GeometriesApi geometryApi, string projectId, Guid geometryId,
                               List<string> values = null)
        {
            var entities = geometryApi.GetGeometryMappings(
                projectId: projectId,
                geometryId: geometryId,
                attributes: new List<string> { "SDL/TYSA_COLOUR" },
                values: values
            ).Embedded;
            if (entities.Count == 15)
            {
                return entities[0].Name;
            }
            else
            {
                throw new Exception("Unexpected number of entities returned");
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
            get { return new Guid("A41F6369-ABF5-4DD8-8244-62D3810C6F15"); }
        }
    }
}