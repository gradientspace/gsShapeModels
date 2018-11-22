using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using g3;

namespace gs
{


	public struct DGTimestamp : IComparable<DGTimestamp>, IEquatable<DGTimestamp>
    {
        const int INVALID_TIME = int.MinValue;

		public int time;
		public DGTimestamp(int timeIn) { time = timeIn; }

        public void Increment() {
            time++;
        }

        static public readonly DGTimestamp Zero = new DGTimestamp(0);
        static public readonly DGTimestamp Invalid = new DGTimestamp(int.MinValue);



        public static bool operator ==(DGTimestamp a, DGTimestamp b) {
            return (a.time == b.time || a.time == INVALID_TIME || b.time == INVALID_TIME );
        }
        public static bool operator !=(DGTimestamp a, DGTimestamp b) {
            return (a.time != b.time || a.time == INVALID_TIME || b.time == INVALID_TIME);
        }
        public override bool Equals(object obj) {
            return this == (DGTimestamp)obj;
        }
        public override int GetHashCode() {
            return time.GetHashCode();
        }
        public int CompareTo(DGTimestamp other) {
            if (time != other.time && time != INVALID_TIME && other.time != INVALID_TIME)
                return time < other.time ? -1 : 1;
            return 0;
        }
        public bool Equals(DGTimestamp other) {
            return (time == other.time || time == INVALID_TIME || other.time == INVALID_TIME);
        }

	}

	public struct DGArguments
	{
		public DGTimestamp Timestamp;
		public int flags;
		public object data;

		static public readonly DGArguments Empty = new DGArguments() { flags = 0, data = null };
	}




    public interface DGPort
    {
        Type PortType { get; }
    }



    public interface DGOutputPort : DGPort
    {
		object Evaluate(DGArguments args);
		DGTimestamp Timestamp { get; }
    }



	public interface DGInputPort : DGPort
	{
		void ConnectToSource(DGOutputPort port);
        DGOutputPort CurrentSource { get; }

		T Value<T>(DGArguments args);
        DGTimestamp Timestamp { get; }
    }




    public class DGPortSet<T>
    {
        List<T> ports;

        public DGPortSet() {
            ports = new List<T>();
        }
        public DGPortSet(T[] portsIn) {
            ports = new List<T>(portsIn);
        }
        public DGPortSet(IEnumerable<T> portsIn) {
            ports = new List<T>(portsIn);
        }

        public int Count {
            get { return ports.Count; }
        }

        public ReadOnlyCollection<T> Ports {
            get { return ports.AsReadOnly(); }
        }

        public void Add(T port) {
            ports.Add(port);
            OnPortsModified(this, EventArgs.Empty);
        }

        public T this[int idx] {
            get { return ports[idx]; }
        }

        public T2 GetPort<T2>(int idx) where T2 : class {
            return Ports[idx] as T2;
        }

        public event EventHandler OnPortsModified;
    }

    public class DGInputPortSet : DGPortSet<DGInputPort>
    {
    }
    public class DGOutputPortSet : DGPortSet<DGOutputPort>
    {
    }





    //public class DGConnection
    //{
    //    DGPort from;
    //    public DGPort From {
    //        get { return from; }
    //        set {
    //            from = value;
    //        }
    //    }

    //    DGPort to;
    //    public DGPort To {
    //        get { return to; }
    //        set {
    //            to = value;
    //        }
    //    }


    //    public DGConnection(DGPort from, DGPort to) {
    //        this.from = from;
    //        this.to = to;
    //    }

    //}


}
