using System;
using System.Collections.Generic;
using System.Linq;
using g3;

namespace gs
{
    public class DoubleOutPort : DGOutputPort
    {
        public Double Value {
            get { return _value; }
            set {
                if ( _value != value ) {
                    _value = value;
                    _timestamp.Increment();
                }
            }
        }
        double _value = 1.0;
        DGTimestamp _timestamp = DGTimestamp.Zero;

        public Type PortType {
            get { return typeof(Double); }
        }

		public virtual object Evaluate(DGArguments args) {
            return _value;
        }

		public virtual DGTimestamp Timestamp {
			get { return _timestamp; }
		}
    }


    public class IntegerOutPort : DGOutputPort
    {
        public int Value {
            get { return _value; }
            set {
                if (_value != value) {
                    _value = value;
                    _timestamp.Increment();
                }
            }
        }
        int _value = 1;
        DGTimestamp _timestamp = DGTimestamp.Zero;

        public Type PortType {
            get { return typeof(int); }
        }

        public virtual object Evaluate(DGArguments args) {
            return _value;
        }

        public virtual DGTimestamp Timestamp {
            get { return _timestamp; }
        }
    }



    public class StringOutPort : DGOutputPort
    {
        public string Value {
            get { return _value; }
            set {
                if (_value != value) {
                    _value = value;
                    _timestamp.Increment();
                }
            }
        }
        string _value = "";
        DGTimestamp _timestamp = DGTimestamp.Zero;

        public Type PortType {
            get { return typeof(string); }
        }

        public virtual object Evaluate(DGArguments args) {
            return _value;
        }

        public virtual DGTimestamp Timestamp {
            get { return _timestamp; }
        }
    }

        

    public class DCurve3OutPort : DGOutputPort
    {
        public DCurve3 Curve;

        public Type PortType {
            get { return typeof(DCurve3); }
        }

        public virtual object Evaluate(DGArguments args) {
            return Curve;
        }

        public virtual DGTimestamp Timestamp {
            get { return new DGTimestamp(Curve.Timestamp); }
        }
    }




    public class DMesh3OutPort : DGOutputPort
    {
        public DMesh3 Mesh;

        public Type PortType {
            get { return typeof(DMesh3); }
        }

        public virtual object Evaluate(DGArguments args) {
            return Mesh;
        }

        public virtual DGTimestamp Timestamp {
            get { return new DGTimestamp(Mesh.Timestamp); }
        }
    }




    public class ExternalDMesh3OutPort : DGOutputPort
    {
        public Func<DGArguments, DMesh3> MeshSourceF;
        public Func<DGTimestamp> TimestampSourceF;

        public Type PortType {
            get { return typeof(DMesh3); }
        }

        public virtual object Evaluate(DGArguments args) {
            return MeshSourceF(args);
        }

        public virtual DGTimestamp Timestamp {
            get { return TimestampSourceF(); }
        }
    }



    public abstract class DGBaseInPort : DGInputPort
	{
		DGOutputPort source;

		public abstract Type PortType { get; }

		public virtual void ConnectToSource(DGOutputPort port) {
			if (port.PortType != this.PortType)
				throw new ArgumentException("DGBaseInPort.ConnectToSource: tried to connect output type " + port.PortType.ToString() + " to to input type " + PortType.ToString());
			source = port;
		}

        public virtual DGOutputPort CurrentSource {
            get { return source; }
        }

        public virtual T Value<T>(DGArguments args) {
			object o = source.Evaluate(args);
			return (T)o;
		}

        public virtual DGTimestamp Timestamp {
            get { return source.Timestamp; }
        }
    }



	public class DoubleInPort : DGBaseInPort {
		override public Type PortType {
			get { return typeof(Double); }
		}
	}
    public class StringInPort : DGBaseInPort {
        override public Type PortType {
            get { return typeof(String); }
        }
    }
    public class IntegerInPort : DGBaseInPort {
        override public Type PortType {
            get { return typeof(int); }
        }
    }

    public class DCurve3InPort : DGBaseInPort
    {
        override public Type PortType {
            get { return typeof(DCurve3); }
        }
    }

    public class DMesh3InPort : DGBaseInPort
    {
        override public Type PortType {
            get { return typeof(DMesh3); }
        }
    }

}
