using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types.Transforms;
using Rhino.Geometry;
using SimScale.Sdk.Api;
using SimScale.Sdk.Model;
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
            pManager.AddGenericParameter("GeometryAPI", "GA", "Geometry API", GH_ParamAccess.item);
            pManager.AddGenericParameter("SimulationAPI", "SA", "Simulation API", GH_ParamAccess.item);
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
            var geometryApi = new GeometriesApi();
            var simulationApi = new SimulationsApi();


            DA.GetData(0, ref projectId);
            DA.GetData(1, ref geometryIdText);
            DA.GetData(2, ref geometryApi);
            DA.GetData(3, ref simulationApi);

            Guid geometryId = new Guid(geometryIdText);

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
            var bc3Entity = getSingleEntityName(
                geometryApi,
                projectId,
                geometryId,
                values: new List<string> { "block4" }
            );
            var bc4Entity = getSingleEntityName(
                geometryApi,
                projectId,
                geometryId,
                values: new List<string> { "Wall 1" }
            );

            // Create geeometry primitive to use as probe point in simulation
            var geometryPrimitivePoint = new Point(
                name: "Point 1",
                center: new DimensionalVectorLength(
                    value: new DecimalVector(
                        x: 0,
                        y: 0,
                        z: -0
                    ),
                    unit: DimensionalVectorLength.UnitEnum.M
                )
            );
            var geometryPrimitiveId = simulationApi.CreateGeometryPrimitive(
                projectId,
                geometryPrimitivePoint
            ).GeometryPrimitiveId;
            Console.WriteLine("geometryPrimitiveUuid: " + geometryPrimitiveId);


            // Create simulation model
            var simulationModel = new ConvectiveHeatTransfer(
                isCompressible: false,
                turbulenceModel: ConvectiveHeatTransfer.TurbulenceModelEnum.KOMEGASST,
                model: new FluidModel(),
                initialConditions: new FluidInitialConditions(),
                advancedConcepts: new AdvancedConcepts(),
                materials: new ConvectiveHeatTransferMaterials(),
                numerics: new FluidNumerics(
                    relaxationFactor: new RelaxationFactor(),
                    pressureReferenceValue: new DimensionalPressure(
                        value: 0,
                        unit: DimensionalPressure.UnitEnum.Pa
                    ),
                    residualControls: new ResidualControls(
                        velocity: new Tolerance(),
                        pressureRgh: new Tolerance(),
                        turbulentKineticEnergy: new Tolerance(),
                        omegaDissipationRate: new Tolerance(),
                        temperature: new Tolerance()
                    ),
                    solvers: new FluidSolvers(),
                    schemes: new Schemes(
                        timeDifferentiation: new TimeDifferentiationSchemes(),
                        gradient: new GradientSchemes(),
                        divergence: new DivergenceSchemes(),
                        laplacian: new LaplacianSchemes(),
                        interpolation: new InterpolationSchemes(),
                        surfaceNormalGradient: new SurfaceNormalGradientSchemes()
                    )
                ),
                boundaryConditions: new List<OneOfConvectiveHeatTransferBoundaryConditions>() {
                new VelocityInletBC(
                    name: "Velocity inlet 1",
                    velocity: new FixedValueVBC(
                        value: new DimensionalVectorFunctionSpeed(
                            value: new ComponentVectorFunction(
                                x: new ConstantFunction(value: 0),
                                y: new ConstantFunction(value: (decimal) -0.001),
                                z: new ConstantFunction(value: 0)
                            )
                        )
                    ),
                    temperature: new FixedValueTBC(
                        value: new DimensionalFunctionTemperature(
                            value: new ConstantFunction(value: (decimal) 19.85),
                            unit: DimensionalFunctionTemperature.UnitEnum.C
                        )
                    ),
                    topologicalReference: new TopologicalReference(
                        entities: new List<string>() { bc1Entity }
                    )
                ),
                new PressureOutletBC(
                    name: "Pressure outlet 2",
                    gaugePressureRgh: new FixedValuePBC(
                        value: new DimensionalFunctionPressure(
                            value: new ConstantFunction(value: 0),
                            unit: DimensionalFunctionPressure.UnitEnum.Pa
                        )
                    ),
                    topologicalReference: new TopologicalReference(
                        entities: new List<string>() { bc2Entity }
                    )
                ),
                new PressureOutletBC(
                    name: "Pressure outlet 3",
                    gaugePressureRgh: new FixedValuePBC(
                        value: new DimensionalFunctionPressure(
                            value: new ConstantFunction(value: 0),
                            unit: DimensionalFunctionPressure.UnitEnum.Pa
                        )
                    ),
                    topologicalReference: new TopologicalReference(
                        entities: new List<string>() { bc3Entity }
                    )
                ),
                new WallBC(
                    name: "Wall 4",
                    velocity: new NoSlipVBC(
                        turbulenceWall: NoSlipVBC.TurbulenceWallEnum.WALLFUNCTION
                    ),
                    temperature: new FixedValueTBC(
                        value: new DimensionalFunctionTemperature(
                            value: new ConstantFunction(value: 285),
                            unit: DimensionalFunctionTemperature.UnitEnum.C
                        )
                    ),
                    topologicalReference: new TopologicalReference(
                        entities: new List<string>() { bc4Entity }
                    )
                )
                },
                simulationControl: new FluidSimulationControl(
                    endTime: new DimensionalTime(
                        value: 100,
                        unit: DimensionalTime.UnitEnum.S
                    ),
                    deltaT: new DimensionalTime(
                        value: 1,
                        unit: DimensionalTime.UnitEnum.S
                    ),
                    maxRunTime: new DimensionalTime(
                        value: 10000,
                        unit: DimensionalTime.UnitEnum.S
                    ),
                    writeControl: new TimeStepWriteControl(
                        writeInterval: 100
                    ),
                    decomposeAlgorithm: new ScotchDecomposeAlgorithm()
                ),
                resultControl: new FluidResultControls(
                    forcesMoments: new List<OneOfFluidResultControlsForcesMoments>(),
                    surfaceData: new List<OneOfFluidResultControlsSurfaceData>() {
                    new AreaAverageResultControl(
                        name: "Area average 1",
                        writeControl: new TimeStepWriteControl(writeInterval: 1),
                        topologicalReference: new TopologicalReference(
                            entities: new List<string>() { bc4Entity }
                        )
                    )
                    },
                    probePoints: new List<ProbePointsResultControl>() {
                    new ProbePointsResultControl(
                        name: "Probe point 1",
                        writeControl: new TimeStepWriteControl(writeInterval: 1),
                        geometryPrimitiveUuids: new List<Guid?>() { geometryPrimitiveId }
                    )
                    }
                )
            );
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