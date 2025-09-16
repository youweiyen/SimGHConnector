using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino.Geometry;
using SimScale.Sdk.Api;
using SimScale.Sdk.Model;

using System.Linq;
using System.Configuration;

namespace SimGH
{
    public class GH_MaterialToEntity : GH_Component, IGH_VariableParameterComponent
    {
        /// <summary>
        /// Initializes a new instance of the GH_MaterialToEntity class.
        /// </summary>
        public GH_MaterialToEntity()
          : base("MaterialToEntity", "E",
              "Get the material geometry name from the uploaded geometry",
              "´SimGH", "1_SimScale")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("ProjectInfo", "I", "SimScale project info", GH_ParamAccess.item);
            pManager.AddTextParameter("MaterialName", "M", "Material Name", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("", "", "", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            SimProjectInfo simProjectInfo = new SimProjectInfo();
            List<string> inNames = new List<string>();

            DA.GetData(0, ref simProjectInfo);
            DA.GetDataList(1, inNames);


            SimScale.Sdk.Client.Configuration config = simProjectInfo.Configuration;
            var geometryApi = new GeometriesApi(config);
            Guid geometryId = simProjectInfo.GeometryId;
            string projectId = simProjectInfo.ProjectId;

            var all = new List<string>();
            for (int i = 0; i < inNames.Count; i++)
            {
                var geometryNames = GetEntityByName(
                    geometryApi,
                    projectId,
                    geometryId,
                    values: new List<string> { inNames[i] }
                );

                DA.SetDataList(i, geometryNames);
            }
        }

        public bool CanInsertParameter(GH_ParameterSide side, int index)
        {
            return side == GH_ParameterSide.Output;
        }
        public bool CanRemoveParameter(GH_ParameterSide side, int index)
        {
            return side == GH_ParameterSide.Output && Params.Output.Count > 1;
        }
        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
        {
            if(side != GH_ParameterSide.Input) return null;

            return new Param_String();
        }
        public void VariableParameterMaintenance()
        {
            for (int i = 0; i < Params.Output.Count; i++)
            {
                var param = Params.Output[i];
                param.Access = GH_ParamAccess.item;

                param.MutableNickName = false;
                param.NickName = $"C{i + 1}";
                param.Name = $"Character {i + 1}.";
                param.Description = $"Character at location {i + 1}.";
            }
        }

        public bool DestroyParameter(GH_ParameterSide side, int index)
        {
            return true;
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
            get { return new Guid("8F4402F9-174E-4272-8BD8-43387D1DD656"); }
        }
    }
}