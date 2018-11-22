using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class EnclosedRegionOffsetOp : ParametricVectorDisplacementMapOp
    {
        Vector3d normal;
        public Vector3d Normal {
            get { return normal; }
            set { normal = value; base.invalidate(); }
        }

        double offset_distance;
        public double PushPullDistance {
            get { return offset_distance; }
            set { offset_distance = value; base.invalidate(); }
        }

        IFalloffFunction falloff;
        public IFalloffFunction Falloff {
            get { return falloff; }
            set { falloff = value.Duplicate(); base.invalidate(); }
        }


        ISampledCurve3dSourceOp curve_source;
        public ISampledCurve3dSourceOp CurveSource {
            get { return curve_source; }
            set {
                if (curve_source != null)
                    curve_source.OperatorModified -= on_source_modified;
                curve_source = value;
                if (curve_source != null )
                    curve_source.OperatorModified += on_source_modified;
                base.invalidate();
            }
        }



        public EnclosedRegionOffsetOp(IMeshSourceOp meshSource = null) : base(meshSource)
        {
            Normal = Vector3d.AxisY;
            offset_distance = 0.25f;
            falloff = new WyvillFalloff() { ConstantRange = 0.25f };
        }


        protected override void Update_GenerateMap()
        {
            base.begin_update();

            if ( MeshSource == null )
                throw new Exception("EnclosedRegionOffsetOp: must set valid MeshSource to compute!");
            if ( MeshSource.HasSpatial == false)
                throw new Exception("EnclosedRegionOffsetOp: MeshSource must have spatial data structure!");

            IMesh imesh = MeshSource.GetIMesh();
            if (imesh.HasVertexNormals == false)
                throw new Exception("EnclosedRegionOffsetOp: input mesh does not have surface normals...");
            if (imesh is DMesh3 == false)
                throw new Exception("EnclosedRegionOffsetOp: in current implementation, input mesh must be a DMesh3. Ugh.");
            DMesh3 mesh = imesh as DMesh3;
            ISpatial spatial = MeshSource.GetSpatial();

            DCurve3 curve = new DCurve3(CurveSource.GetICurve());
            MeshFacesFromLoop loop = new MeshFacesFromLoop(mesh, curve, spatial);
          
            // [RMS] this is all f'n ugly!

            MeshVertexSelection selection = new MeshVertexSelection(mesh);
            selection.SelectTriangleVertices(loop.InteriorTriangles);


            // [TODO] do this inline w/ loop below? but then no maxdist!
            Dictionary<int, double> dists = new Dictionary<int, double>();
            double max_dist = 0;
            foreach ( int vid in selection ) {
                Vector3d v = mesh.GetVertex(vid);
                int inearseg; double nearsegt;
                double min_dist_sqr = curve.DistanceSquared(v, out inearseg, out nearsegt);
                min_dist_sqr = Math.Sqrt(min_dist_sqr);
                max_dist = Math.Max(min_dist_sqr, max_dist);
                dists[vid] = min_dist_sqr;
            }


            lock (Displacement) {
                Displacement.Clear();
                Displacement.Resize(mesh.MaxVertexID);

                // todo: can do this in parallel...
                foreach (int vid in selection) {
                    //Vector3d v = mesh.GetVertex(vid);

                    // [TODO]...
                    double dist = max_dist - dists[vid];
                    double falloff = Falloff.FalloffT(dist / max_dist);

                    Vector3d n = mesh.GetVertexNormal(vid);
                    n = n - n.Dot(normal) * normal;
                    n.Normalize();

                    Displacement[vid] = falloff * offset_distance * n;
                }
            }

            // smooth it?

            base.complete_update();
        }



    }
}
