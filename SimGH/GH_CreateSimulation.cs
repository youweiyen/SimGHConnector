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
using Grasshopper.Kernel.Parameters;

namespace SimGH
{
    public class GH_CreateSimulation : GH_Component, IGH_VariableParameterComponent
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
            pManager.AddBooleanParameter("Create", "T", "Set true to create simulation", GH_ParamAccess.item);
            pManager.AddTextParameter("MaterialName", "N", "Material Name", GH_ParamAccess.list);
            pManager.AddNumberParameter("Flux", "F", "Surface heat flux value. Unit: W/m2", GH_ParamAccess.item);
            pManager.AddNumberParameter("Temperature", "T", "Convective heat flux reference temperature. Unit: Celcius ", GH_ParamAccess.item);
            pManager.AddNumberParameter("Coefficient", "C", "Convective heat transfer coefficient. Unit: W/(km2) ", GH_ParamAccess.item);
            pManager.AddTextParameter("Material1", "M1", "Material", GH_ParamAccess.list);
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
            DA.GetData(1, ref create);
            DA.GetDataList(2, materialName);
            DA.GetData(3, ref inFlux);
            DA.GetData(4, ref inTemperature);
            DA.GetData(5, ref inCoefficient);

            var materialEntities = new List<List<string>>();
            for (int i = 6; i < Params.Input.Count; i++)
            {
                List<string> items = new List<string>();
                DA.GetDataList(i, items);
                materialEntities.Add(items);
            }

            if(create)
            {
                Message = "Creating...";

                SimulationsApi simulationApi = new SimulationsApi();
                Guid? simulationId = default;


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
                        new Contact(nodeMergingBonded: true, connections: new List<OneOfContactConnections>()) 
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
                
                // Add assignments to the new material
                
                for (int i = 0; i < materialName.Count; i++)
                {
                    var materialUpdateResponse = UpdateMaterial(projectId, simulationId, simulationApi, materialsApi,
                                            customMaterialGroup, customMaterials, materialName[i]);
                }

                simulationSpec = simulationApi.GetSimulation(projectId, simulationId);
                var materials = ((HeatTransfer)simulationSpec.Model).Materials;
                
                for (int i = 0; i < materialName.Count; i++)
                {
                    materials.Where(m => m.Name == materialName[i])
                             .First().TopologicalReference = new TopologicalReference(entities: materialEntities[i]);
                }

                simulationApi.UpdateSimulation(projectId, simulationId, simulationSpec);

                simProjectInfo.SetSimulationId(simulationId);
                simProjectInfo.SetSimulationApi(simulationApi);
                simProjectInfo.SetSimulationSpec(simulationSpec);

            }

            DA.SetData(0, simProjectInfo);
            if (simProjectInfo.SimulationId != default)
            { 
                Message = "Created Simulation";
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

        public bool CanInsertParameter(GH_ParameterSide side, int index)
        {
            return side == GH_ParameterSide.Input;
        }

        public bool CanRemoveParameter(GH_ParameterSide side, int index)
        {
            return side == GH_ParameterSide.Input && Params.Input.Count > 7;
        }

        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
        {
            if (side != GH_ParameterSide.Input) return null;

            return new Param_String();
        }

        public bool DestroyParameter(GH_ParameterSide side, int index)
        {
            return true;
        }

        public void VariableParameterMaintenance()
        {
            for (int i = 6; i < Params.Input.Count; i++)
            {
                var param = Params.Input[i];
                param.Access = GH_ParamAccess.list;

                param.MutableNickName = false;
                param.NickName = $"M{i - 6 + 1}";
                param.Name = $"Material {i - 6 + 1}";
                param.Description = $"Material Entity {i - 6 + 1}";
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