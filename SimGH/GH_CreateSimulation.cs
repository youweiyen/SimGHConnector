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
          : base("CreateSim", "Create",
              "Create Simulation Model",
              "SimGH", "1_SimScale")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("ProjectInfo", "I", "SimScale Project Info", GH_ParamAccess.item);
            pManager.AddTextParameter("MaterialName", "N", "Material Name", GH_ParamAccess.list);
            pManager.AddNumberParameter("Flux", "F", "Surface heat flux value. Unit: W/m2", GH_ParamAccess.item);
            pManager.AddNumberParameter("Temperature", "T", "Convective heat flux reference temperature. Unit: Celcius ", GH_ParamAccess.item);
            pManager.AddNumberParameter("Coefficient", "C", "Convective heat transfer coefficient. Unit: W/(km2) ", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Create", "T", "Set true to create simulation", GH_ParamAccess.item);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("ProjectInfo", "I", "SimScale Project Info", GH_ParamAccess.item);
        }


        SimProjectInfo simProjectInfo = new SimProjectInfo();

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double inFlux = default;
            double inTemperature = default;
            double inCoefficient = default;

            List<string> materialName = new List<string>();
            bool create = false;

            DA.GetData(0, ref simProjectInfo);
            DA.GetDataList(1, materialName);
            DA.GetData(2, ref inFlux);
            DA.GetData(3, ref inTemperature);
            DA.GetData(4, ref inCoefficient);
            DA.GetData(5, ref create);


            if(create)
            {

                SimulationsApi simulationApi = new SimulationsApi();
                Guid? simulationId = default;

                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Running");

                Guid geometryId = simProjectInfo.GeometryId;
                string projectId = simProjectInfo.ProjectId;

                SimScale.Sdk.Client.Configuration config = simProjectInfo.Configuration;

                var geometryApi = new GeometriesApi(config);
                var meshOperationApi = new MeshOperationsApi(config);
                simulationApi = new SimulationsApi(config);
                var materialsApi = new MaterialsApi(config);

                //var simulationRunApi = new SimulationRunsApi(config);
                //var tableImportApi = new TableImportsApi(config);
                //var reportsApi = new ReportsApi(config);

                // Read geometry information and update with the deserialized model
                var geometry = geometryApi.GetGeometry(projectId, geometryId);
                geometryApi.UpdateGeometry(projectId, geometryId, geometry);

                // Get geometry mappings

                //var bc1Entity = geometryApi.GetGeometryMappings(
                //    projectId: projectId,
                //    geometryId: geometryId,
                //    _class: "face"
                //).Embedded;
                //var bc2entity = geometryApi.GetGeometryMappings(
                //    projectId: projectId,
                //    geometryId: geometryId,
                //    _class: "region"
                //).Embedded;

                var geometry0 = GetEntityByName(
                    geometryApi,
                    projectId,
                    geometryId,
                    values: new List<string> { materialName[0] }
                );
                var geometry1 = GetEntityByName(
                    geometryApi,
                    projectId,
                    geometryId,
                    values: new List<string> { materialName[1] }
                );
                var geometry2 = GetEntityByName(
                    geometryApi,
                    projectId,
                    geometryId,
                    values: new List<string> { materialName[2] }
                );
                var heatEntity = GetEntityByColor(
                    geometryApi,
                    projectId,
                    geometryId,
                    values: new List<string> { "[1 0 0]" }
                );
                var convectionEntity = GetEntityByColor(
                    geometryApi,
                    projectId,
                    geometryId,
                    values: new List<string> { "[0 0 1]" }
                );


                // Initialize simulation model
                var simulationModel = new HeatTransfer (
                    timeDependency: default,
                    nonLinearAnalysis: false,
                    connectionGroups: new List<Contact>() 
                    {
                        //new Contact(nodeMergingBonded: true, connections: new List<OneOfContactConnections>() { new BondedContact}) 
                    },
                    elementTechnology: new SolidElementTechnology(new ElementTechnology(new AutomaticElementDefinitionMethod())),
                    model: new SolidModel(),
                    materials: new List<SolidMaterial>(),
                    initialConditions: new SolidInitialConditions(),
                    boundaryConditions: 
                    new List<OneOfHeatTransferBoundaryConditions>()
                    {
                        new SurfaceHeatFluxBC(
                            name: "SurfaceHeatFlux",
                            heatfluxValue: new DimensionalFunctionHeatFlux(
                                value: new ConstantFunction(value: (decimal) inFlux), 
                                unit: DimensionalFunctionHeatFlux.UnitEnum.WM),
                            topologicalReference: new TopologicalReference(entities: heatEntity)),

                        new ConvectiveHeatFluxBC(
                            name: "ConvectiveHeatFlux",
                            referenceTemperature: new DimensionalFunctionTemperature(
                                value: new ConstantFunction(value :(decimal) inTemperature),
                                unit: DimensionalFunctionTemperature.UnitEnum.C),
                            heatTransferCoefficient: new DimensionalFunctionThermalTransmittance(
                                value: new ConstantFunction(value :(decimal) inCoefficient),
                                unit: DimensionalFunctionThermalTransmittance.UnitEnum.WKm),
                            topologicalReference: new TopologicalReference(entities: convectionEntity))

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
                var simulationSpec = new SimulationSpec(name: "Heat_Transfer", geometryId: geometryId, model: simulationModel);

                // Create simulation first to use for physics based meshing
                simulationId = simulationApi.CreateSimulation(projectId, simulationSpec).SimulationId;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "simulationId: " + simulationId);

                // Add a material to the simulation
                var materialGroups = materialsApi.GetMaterialGroups().Embedded;

                var customMaterialGroup = materialGroups.FirstOrDefault(group => group.GroupType == MaterialGroupType.USERCUSTOM);
                if (customMaterialGroup == null)
                {
                    throw new Exception("Couldn't find default material group in " + materialGroups);
                }

                var customMaterials = materialsApi.GetMaterials(customMaterialGroup.MaterialGroupId).Embedded;
            

                var material0UpdateResponse = UpdateMaterial(projectId, simulationId, simulationApi, materialsApi, 
                                                            customMaterialGroup, customMaterials, materialName[0]);
                var material1UpdateResponse = UpdateMaterial(projectId, simulationId, simulationApi, materialsApi,
                                                customMaterialGroup, customMaterials, materialName[1]);
                var material2UpdateResponse = UpdateMaterial(projectId, simulationId, simulationApi, materialsApi,
                                                customMaterialGroup, customMaterials, materialName[2]);

                // Add assignments to the new material
                simulationSpec = simulationApi.GetSimulation(projectId, simulationId);
                var materials = ((HeatTransfer)simulationSpec.Model).Materials;

                var material0 = materials.Where(m => m.Name == materialName[0]).First();
                var material1 = materials.Where(m => m.Name == materialName[1]).First();
                var material2 = materials.Where(m => m.Name == materialName[2]).First();

                material0.TopologicalReference = new TopologicalReference(entities: geometry0);
                material1.TopologicalReference = new TopologicalReference(entities: geometry1);
                material2.TopologicalReference = new TopologicalReference(entities: geometry2);

                simulationApi.UpdateSimulation(projectId, simulationId, simulationSpec);

                // Create mesh operation
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

                simProjectInfo.SetSimulationId(simulationId);
                simProjectInfo.SetSimulationApi(simulationApi);

            }

            DA.SetData(0, simProjectInfo);

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Created Simulation");

        }


        public static List<string> GetEntityByName(GeometriesApi geometryApi, string projectId, Guid geometryId,
                                       List<string> values = null)
        {
            var entities = geometryApi.GetGeometryMappings(
                projectId: projectId,
                geometryId: geometryId,
                attributes: new List<string> { "ATTRIB_XPARASOLID_NAME" },
                values: values
            ).Embedded;

            if (entities.Count != 0)
            {
                List<string> names = entities.Select(e => e.Name).ToList();
                return names;
            }
            else
            {
                throw new Exception("No entities returned");
            }
        }
        public static List<string> GetEntityByColor(GeometriesApi geometryApi, string projectId, Guid geometryId,
                               List<string> values = null)
        {
            var entities = geometryApi.GetGeometryMappings(
                projectId: projectId,
                geometryId: geometryId,
                attributes: new List<string> { "SDL/TYSA_COLOUR" },
                values: values
            ).Embedded;

            if (entities.Count != 0)
            {
                List<string> names = entities.Select(e => e.Name).ToList();
                return names;
            }
            else
            {
                throw new Exception("No entities returned");
            }
        }

        public MaterialUpdateResponse UpdateMaterial(string projectId, Guid? simulationId, SimulationsApi simulationApi, MaterialsApi materialsApi, MaterialGroupResponse customMaterialGroup, List<MaterialResponse> customMaterials, string materialName)
        {
            var importMaterial = customMaterials.FirstOrDefault(material => material.Name == materialName);
            if (importMaterial == null)
            {
                throw new Exception("Couldn't find default Wood material in " + customMaterials);
            }

            var materialData = materialsApi.GetMaterialData(customMaterialGroup.MaterialGroupId, importMaterial.Id);

            var materialUpdateRequest = new MaterialUpdateRequest(
            operations: new List<MaterialUpdateOperation> {
                            new MaterialUpdateOperation(
                                path: "/materials",
                                materialData: materialData,
                                reference: new MaterialUpdateOperationReference(
                                    materialGroupId: customMaterialGroup.MaterialGroupId,
                                    materialId: importMaterial.Id
                                )
                            )
                            }
            );

            var materialUpdateResponse = simulationApi.UpdateSimulationMaterials(projectId, simulationId, materialUpdateRequest);

            return materialUpdateResponse;
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