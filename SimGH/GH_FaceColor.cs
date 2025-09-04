using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace SimGH
{
    public class GH_FaceColor : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GH_AssignFaceAttribute class.
        /// </summary>
        public GH_FaceColor()
          : base("FaceColor", "",
              "Description",
              "Category", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Brep", "B", "Brep", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Brep", "B", "Brep", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep wood = new Brep();

            DA.GetData(0, ref wood);

            foreach (var wood in stock)
            {
                double lowHeight = double.MaxValue;
                double highHeight = double.MinValue;

                BrepFace lowestSurface = default;
                BrepFace highestSurface = default;
                for (int i = 0; i < wood.Faces.Count; i++)
                {
                    BrepFace surface = wood.Faces[i];
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
                lowestSurface.PerFaceColor = Color.Red;
                highestSurface.PerFaceColor = Color.Blue;
            }

            a = stock;

            DA.SetData(0, wood);
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
}