using RestSharp;
using SimScale.Sdk.Api;
using SimScale.Sdk.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimGH
{
    public class SimProjectInfo
    {
        public SimProjectInfo() { }

        public SimulationsApi SimulationApi { get; private set; }
        public Guid? SimulationId { get; private set; }
        public GeometriesApi GeometryApi  { get; private set; }
        public MeshOperationsApi MeshOperationApi { get; private set; }
        public MaterialsApi MaterialsApi { get; private set; }
        public SimulationRunsApi SimulationRunApi { get; private set; }
        public TableImportsApi TableImportApi { get; private set; }
        public ReportsApi ReportsApi { get; private set; }
        public string ProjectName {  get; private set; }
        public Configuration Configuration { get; private set; }

        public void SetSimulationApi(SimulationsApi simulationsApi) => SimulationApi = simulationsApi;
        public void SetSimulationId(Guid? guid) => SimulationId = guid;
        public void SetGeometriesApi(GeometriesApi geometryApi) => GeometryApi = geometryApi;
        public void SetMeshOperationsApi(MeshOperationsApi meshOperationApi) => MeshOperationApi = meshOperationApi;
        public void SetMaterialsApi(MaterialsApi materialsApi) => MaterialsApi = materialsApi;
        public void SetSimulationRunsApi(SimulationRunsApi simulationRunApi) => SimulationRunApi = simulationRunApi;
        public void SetTableImportApi(TableImportsApi tableImportApi) => TableImportApi = tableImportApi;
        public void SetReportsApi(ReportsApi reportsApi) => ReportsApi = reportsApi;
        public void SetProjectName(string projectName) => ProjectName = projectName;
        public void SetConfiguration(Configuration configuration) => Configuration = configuration;

    }
}
