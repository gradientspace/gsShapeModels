// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class LengthenOp : BaseModelingOperator, IVectorDisplacementSourceOp
    {
        VectorDisplacement Displacement;

        Vector3d basePt;
        public Vector3d BasePoint {
            get { return basePt; }
            set { basePt = value; on_modified(); }
        }

        Vector3d direction;
        public Vector3d Direction {
            get { return direction; }
            set { direction = value; on_modified(); }
        }

        double band_distance;
        public double BandDistance {
            get { return band_distance; }
            set { band_distance = Math.Max(0.001, value); on_modified(); }
        }

        double falloff_rate;
        public double FalloffRate {
            get { return falloff_rate; }
            set { falloff_rate = MathUtil.Clamp(value, 0, 1); on_modified(); }
        }

        double distance;
        public double LengthenDistance {
            get { return distance; }
            set { distance = value; on_modified(); }
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


        public LengthenOp(IMeshSourceOp meshSource = null)
        {
            basePt = Vector3d.Zero;
            Direction = -Vector3d.AxisY;
            distance = 1.0;
            falloff_rate = 0.25;
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
                throw new Exception("LengthenOp: must set valid MeshSource to compute!");

            IMesh mesh = MeshSource.GetIMesh();
            if (mesh.HasVertexNormals == false)
                throw new Exception("LengthenOp: input mesh does not have surface normals...");

            Displacement.Resize(mesh.MaxVertexID);

            // todo: can do this in parallel
            foreach (int vid in mesh.VertexIndices()) {
                Vector3d v = mesh.GetVertex(vid);
                double dist = (v - basePt).Dot(direction);
                if (dist < -band_distance)
                    continue;

                dist = Math.Abs(Math.Min(0, dist));
                double falloff_dist = falloff_rate * band_distance;
                double t = MathUtil.WyvillFalloff(dist, falloff_dist, band_distance);

                Displacement[vid] = t * distance * direction;
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
