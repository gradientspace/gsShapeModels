// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class PlaneBandExpansionOp : BaseModelingOperator, IVectorDisplacementSourceOp
    {
        VectorDisplacement Displacement;

        Vector3d origin;
        public Vector3d Origin {
            get { return origin; }
            set { origin = value; on_modified(); }
        }

        Vector3d normal;
        public Vector3d Normal {
            get { return normal; }
            set { normal = value; on_modified(); }
        }

        double band_distance;
        public double BandDistance {
            get { return band_distance; }
            set { band_distance = value; on_modified(); }
        }

        double offset_distance;
        public double PushPullDistance {
            get { return offset_distance; }
            set { offset_distance = value; on_modified(); }
        }


        IMeshSourceOp mesh_source;
        public IMeshSourceOp MeshSource {
            get { return mesh_source; }
            set {
                if (mesh_source != null)
                    mesh_source.OperatorModified -= on_source_modified;
                mesh_source = value;
                if ( mesh_source != null )
                    mesh_source.OperatorModified += on_source_modified;
                on_modified();
            }
        }


        public PlaneBandExpansionOp(IMeshSourceOp meshSource = null)
        {
            origin = Vector3d.Zero;
            Normal = Vector3d.AxisY;
            band_distance = 1.0f;
            offset_distance = 0.25f;
            Displacement = new VectorDisplacement();
            if ( meshSource != null )
                MeshSource = meshSource;
        }


        bool result_valid = false;

        protected virtual void on_modified()
        {
            result_valid = false;
            PostOnOperatorModified();
        }
        protected virtual void on_source_modified(ModelingOperator op)
        {
            on_modified();
        }



        public virtual void Update()
        {
            if ( MeshSource == null )
                throw new Exception("PlaneBandExpansionOp: must set valid MeshSource to compute!");

            IMesh mesh = MeshSource.GetIMesh();
            if (mesh.HasVertexNormals == false)
                throw new Exception("PlaneBandExpansionOp: input mesh does not have surface normals...");

            Displacement.Resize(mesh.MaxVertexID);

            // todo: can do this in parallel
            foreach (int vid in mesh.VertexIndices()) {
                Vector3d v = mesh.GetVertex(vid);
                double dist = (v - origin).Dot(normal);
                dist = Math.Abs(dist);
                if (dist > band_distance)
                    continue;

                double falloff = MathUtil.WyvillFalloff(dist, band_distance * 0.1, band_distance);

                Vector3d n = mesh.GetVertexNormal(vid);
                n = n - n.Dot(normal) * normal;
                n.Normalize();

                Displacement[vid] = falloff * offset_distance * n;
            }

            result_valid = true;
        }


        public IVectorDisplacement GetDisplacement()
        {
            if (result_valid == false)
                Update();

            return Displacement;
        }
    }
}
