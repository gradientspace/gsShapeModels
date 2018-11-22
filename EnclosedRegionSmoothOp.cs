// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class EnclosedRegionSmoothOp : BaseModelingOperator, IVectorDisplacementSourceOp
    {
        VectorDisplacement Displacement;

        double smooth_alpha = 0.5;
        public double SmoothAlpha {
            get { return smooth_alpha; }
            set { smooth_alpha = MathUtil.Clamp(value, 0, 1); on_modified(); }
        }


        double offset_distance = 0;
        public double OffsetDistance {
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


        ISampledCurve3dSourceOp curve_source;
        public ISampledCurve3dSourceOp CurveSource {
            get { return curve_source; }
            set {
                if (curve_source != null)
                    curve_source.OperatorModified -= on_source_modified;
                curve_source = value;
                if (curve_source != null )
                    curve_source.OperatorModified += on_source_modified;
                on_modified();
            }
        }


        public EnclosedRegionSmoothOp(IMeshSourceOp meshSource = null)
        {
            smooth_alpha = 0.5f;
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
                throw new Exception("EnclosedRegionOffsetOp: must set valid MeshSource to compute!");
            if ( MeshSource.HasSpatial == false)
                throw new Exception("EnclosedRegionOffsetOp: MeshSource must have spatial data structure!");

            IMesh imesh = MeshSource.GetIMesh();
            if (imesh.HasVertexNormals == false)
                throw new Exception("EnclosedRegionOffsetOp: input mesh does not have surface normals...");
            if (imesh is DMesh3 == false)
                throw new Exception("RegionOffsetOp: in current implementation, input mesh must be a DMesh3. Ugh.");
            DMesh3 mesh = imesh as DMesh3;
            ISpatial spatial = MeshSource.GetSpatial();

            DCurve3 curve = new DCurve3(CurveSource.GetICurve());
            MeshFacesFromLoop loop = new MeshFacesFromLoop(mesh, curve, spatial);

            // extract submesh
            RegionOperator op = new RegionOperator(mesh, loop.InteriorTriangles);
            DMesh3 submesh = op.Region.SubMesh;

            // find boundary verts and nbr ring
            HashSet<int> boundaryV = new HashSet<int>(MeshIterators.BoundaryEdgeVertices(submesh));
            HashSet<int> boundaryNbrs = new HashSet<int>();
            foreach ( int vid in boundaryV ) {
                foreach ( int nbrvid in submesh.VtxVerticesItr(vid) ) {
                    if (boundaryV.Contains(nbrvid) == false)
                        boundaryNbrs.Add(nbrvid);
                }
            }

            // [TODO] maybe should be not using vertex normal here? 
            // use an averaged normal, or a constant for patch?

            // offset mesh if requested
            if (Math.Abs(offset_distance) > 0.0001) {
                foreach (int vid in submesh.VertexIndices()) {
                    if (boundaryV.Contains(vid))
                        continue;
                    // if inner ring is non-zero, then it gets preserved below, and
                    // creates a crease...
                    //double dist = boundaryNbrs.Contains(vid) ? (offset_distance / 2) : offset_distance;
                    double dist = boundaryNbrs.Contains(vid) ? 0 : offset_distance;
                    submesh.SetVertex(vid,
                        submesh.GetVertex(vid) + (float)dist * submesh.GetVertexNormal(vid));
                }
            }


            //double t = MathUtil.Clamp(1.0 - SmoothAlpha, 0.1, 1.0);
            double t = 1.0 - SmoothAlpha;
            t = t * t;
            double boundary_t = 5.0;
            double ring_t = 1.0;

            // smooth submesh, with boundary-ring constraints
            LaplacianMeshSmoother smoother = new LaplacianMeshSmoother(submesh);
            foreach ( int vid in submesh.VertexIndices() ) {
                if (boundaryV.Contains(vid)) {
                    smoother.SetConstraint(vid, submesh.GetVertex(vid), boundary_t, true);
                } else if (boundaryNbrs.Contains(vid)) {
                    smoother.SetConstraint(vid, submesh.GetVertex(vid), ring_t);
                } else {
                    smoother.SetConstraint(vid, submesh.GetVertex(vid), t);
                }
            }
            smoother.SolveAndUpdateMesh();


            // turn into displacement vectors
            Displacement.Clear();
            Displacement.Resize(mesh.MaxVertexID);
            foreach (int subvid in op.Region.SubMesh.VertexIndices()) {
                Vector3d subv = op.Region.SubMesh.GetVertex(subvid);
                int basevid = op.Region.SubToBaseV[subvid];
                Vector3d basev = op.Region.BaseMesh.GetVertex(basevid);
                Displacement[basevid] = subv - basev;
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
