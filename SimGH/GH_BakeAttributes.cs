using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using SimScale.Sdk.Model;

namespace SimGH
{
    public class GH_BakeAttributes : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GH_AssignFaceAttribute class.
        /// </summary>
        public GH_BakeAttributes()
          : base("BakeAttributes", "Bake",
              "Set Colors to condition surfaces, and Material Names to Geometry",
              "SimGH", "0_Rhino")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Extrusion", "E", "Geometry Extrusion", GH_ParamAccess.tree);
            pManager.AddTextParameter("´MaterialName", "N", "Material Name", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Run", "T", "Set True to run", GH_ParamAccess.item);
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
            GH_Structure<GH_Brep> stockGoo = new GH_Structure<GH_Brep>();
            List<string> inName = new List<string>();
            bool run = false;

            DA.GetDataTree(0, out stockGoo);
            DA.GetDataList(1, inName);
            DA.GetData(2, ref run);

            string layerName = "Export_STEP";

            if (run)
            { 

                Rhino.RhinoDoc doc = Rhino.RhinoDoc.ActiveDoc;
                var layer = doc.Layers.FindName(layerName, RhinoMath.UnsetIntIndex);
                if (layer == null)
                {
                    doc.Layers.Add(layerName, System.Drawing.Color.Black);
                }
                int layerIndex = doc.Layers.FindName(layerName, RhinoMath.UnsetIntIndex).Index;


                DataTree<Brep> tree = new DataTree<Brep>();
            
                for (int p = 0; p < stockGoo.PathCount; p++)
                {
                    List<GH_Brep> listGoo = stockGoo.Branches[p];
                    List<Brep> stock = listGoo.Select(goo => goo.Value).ToList();

                    for (int w = 0; w < stock.Count; w++)
                    {
                        double lowHeight = double.MaxValue;
                        double highHeight = double.MinValue;

                        BrepFace lowestSurface = default;
                        BrepFace highestSurface = default;

                        for (int i = 0; i < stock[w].Faces.Count; i++)
                        {
                            BrepFace surface = stock[w].Faces[i];
                            double midU = surface.Domain(0).Mid;
                            double midV = surface.Domain(1).Mid;

                            Point3d midPoint = surface.PointAt(midU, midV);
                            if (midPoint.Z < lowHeight)
                            {
                                lowHeight = midPoint.Z;
                                lowestSurface = surface;
                            }
                            if (midPoint.Z > highHeight)
                            {
                                highHeight = midPoint.Z;
                                highestSurface = surface;
                            }
                        }
                        lowestSurface.PerFaceColor = System.Drawing.Color.Red;
                        highestSurface.PerFaceColor = System.Drawing.Color.Blue;
                    
                        ObjectAttributes objAtt = new ObjectAttributes();
                        objAtt.Name = inName[p];
                        objAtt.LayerIndex = layerIndex;
                        doc.Objects.AddBrep(stock[w], objAtt);
                    }
                }
            }

            //tree.AddRange(stock, new GH_Path(p));
            //var objAttribute = new ObjectAttributes();
            //objAttribute.la = 2;
            //objName.Name = inName[p];
            //Guid objId = listGoo[w].ReferenceID;

            //RhinoObject rhinoObject = RhinoDoc.ActiveDoc.Objects.Find(objId);
            //rhinoObject.Attributes = objName;
            //rhinoObject.CommitChanges();

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
            get { return new Guid("98257138-4BED-4021-A840-7ED5E28E9C14"); }
        }
    }
    public class BrepUserData : Rhino.DocObjects.Custom.UserData
    {
        private string brepResults; //Storing the results that names the brep

        public BrepUserData()
        {
            brepResults = default;
        }
        public void AddResults(string result)
        {
            brepResults = result;

        }
    }
}