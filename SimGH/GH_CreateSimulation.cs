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
            pManager.AddTextParameter("ProjectID", "P", "ProjectID", GH_ParamAccess.item);
            pManager.AddTextParameter("GeometryID", "G", "GeometryID", GH_ParamAccess.item);
            pManager.AddGenericParameter("Configuration", "C", "API Client Configuration", GH_ParamAccess.item);
            pManager.AddTextParameter("ProjectName", "N", "Project Name", GH_ParamAccess.item);
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
            string geometryIdText = default;
            SimScale.Sdk.Client.Configuration config = default;
            string projectName = default;

            DA.GetData(0, ref projectId);
            DA.GetData(1, ref geometryIdText);
            DA.GetData(2, ref config);
            DA.GetData(3, ref projectName);

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
            var materialEntity = getSingleEntityName(
                geometryApi,
                projectId,
                geometryId,
                values: new List<string> { "block1" }
            );
            var bc1Entity = getSingleEntityName(
                geometryApi,
                projectId,
                geometryId,
                values: new List<string> { "block2" }
            );
            var bc2Entity = getSingleEntityName(
                geometryApi,
                projectId,
                geometryId,
                values: new List<string> { "block3" }
            );


            // Initialize simulation model
            var simulationModel = new HeatTransfer (
                timeDependency: default,
                nonLinearAnalysis: false,
                connectionGroups: new List<Contact>(),
                elementTechnology: new SolidElementTechnology(),
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
                            entities: new List<string>() { bc1Entity})
                        ),
                    new FixedTemperatureValueBC(
                        name: "SurfaceTemperature",
                        temperatureValue: new DimensionalFunctionTemperature(),
                        topologicalReference: new TopologicalReference(
                            entities: new List<string>() { bc2Entity})
                        ),
                },
                numerics: new SolidNumerics(),
                simulationControl: new SolidSimulationControl(),
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
            var materialAir = defaultMaterials.FirstOrDefault(material => material.Name == "Air");
            if (materialAir == null)
            {
                throw new Exception("Couldn't find default Air material in " + defaultMaterials);
            }

            var materialData = materialsApi.GetMaterialData(defaultMaterialGroup.MaterialGroupId, materialAir.Id);
            var materialUpdateRequest = new MaterialUpdateRequest(
                operations: new List<MaterialUpdateOperation> {
                new MaterialUpdateOperation(
                    path: "/materials/fluids",
                    materialData: materialData,
                    reference: new MaterialUpdateOperationReference(
                        materialGroupId: defaultMaterialGroup.MaterialGroupId,
                        materialId: materialAir.Id
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
                name: "Pipe junction",
                geometryId: geometryId,
                model: new SimmetrixMeshingFluid(
                    physicsBasedMeshing: true,
                    automaticLayerSettings: new AutomaticLayerOn()
                )
            ));
            var meshOperationId = meshOperation.MeshOperationId;
            Console.WriteLine("meshOperationId: " + meshOperationId);
        }


        public static string getSingleEntityName(GeometriesApi geometryApi, string projectId, Guid geometryId,
                                       List<string> values = null)
        {
            var entities = geometryApi.GetGeometryMappings(
                projectId: projectId,
                geometryId: geometryId,
                attributes: new List<string> { "SDL/TYSA_NAME" },
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