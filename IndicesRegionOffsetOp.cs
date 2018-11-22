// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class IndicesRegionOffsetOp : BaseModelingOperator, IVectorDisplacementSourceOp
    {
        VectorDisplacement Displacement;

        Vector3d normal;
        public Vector3d Normal {
            get { return normal; }
            set { normal = value; on_modified(); }
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
                if (mesh_source != null)
                    mesh_source.OperatorModified += on_source_modified;
                on_modified();
            }
        }


        IIndexListSourceOp index_source;
        public IIndexListSourceOp IndexSource {
            get { return index_source; }
            set {
                if (index_source != null)
                    index_source.OperatorModified -= on_source_modified;
                index_source = value;
                if (index_source != null )
                    index_source.OperatorModified += on_source_modified;
                on_modified();
            }
        }


        public IndicesRegionOffsetOp(IMeshSourceOp meshSource = null)
        {
            Normal = Vector3d.AxisY;
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

            IMesh imesh = MeshSource.GetIMesh();
            if (imesh.HasVertexNormals == false)
                throw new Exception("PlaneBandExpansionOp: input mesh does not have surface normals...");
            if (imesh is DMesh3 == false)
                throw new Exception("RegionOffsetOp: in current implementation, input mesh must be a DMesh3. Ugh.");
            DMesh3 mesh = imesh as DMesh3;

            IList<int> faces = IndexSource.GetIndices();

            // [RMS] this is all f'n ugly!

            MeshVertexSelection selection = new MeshVertexSelection(mesh);
            selection.SelectTriangleVertices(faces);

            // ugly
            List<Vector2d> seeds = new List<Vector2d>();
            foreach ( int vid in selection ) {
                foreach (int nbrvid in mesh.VtxVerticesItr(vid)) {
                    if ( selection.IsSelected(nbrvid) == false ) {
                        seeds.Add(new Vector2d(vid, 0));
                        break;
                    }
                }
            }
            Func<int, int, float> distanceF = (a, b) => { return (float)mesh.GetVertex(a).Distance(mesh.GetVertex(b)); };
            Func<int, bool> nodeF = (vid) => { return selection.IsSelected(vid); };
            DijkstraGraphDistance dijkstra = new DijkstraGraphDistance(mesh.MaxVertexID, true, nodeF, distanceF, mesh.VtxVerticesItr, seeds);
            dijkstra.Compute();
            float maxDist = dijkstra.MaxDistance;


            Displacement.Clear();
            Displacement.Resize(mesh.MaxVertexID);
            

            // todo: can do this in parallel...
            foreach (int vid in selection) {
                //Vector3d v = mesh.GetVertex(vid);

                // [TODO]...
                double dist = maxDist - dijkstra.GetDistance(vid);
                double falloff = MathUtil.WyvillFalloff(dist, maxDist * 0.0, maxDist);

                Vector3d n = mesh.GetVertexNormal(vid);
                n = n - n.Dot(normal) * normal;
                n.Normalize();

                Displacement[vid] = falloff * offset_distance * n;
            }

            // smooth it?

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
