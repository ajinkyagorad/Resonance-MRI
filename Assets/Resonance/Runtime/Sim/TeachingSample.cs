using System;
namespace Nebulytic.Resonance.Sim
{
    // Normalized Bloch examples. x,y,z are normalized grid coordinates [-.5,.5].
    // T1/T2 are illustrative seconds, not estimates for a named tissue.
    public static class TeachingSample
    {
        public static D3 Moment(string mode, double u, double x, double y, double z)
        {
            u=Math.Max(0,Math.Min(1,u)); double a=1,theta=Math.PI/2,phase=0,mz=0;
            switch(mode)
            {
                case "single": theta=Math.PI/4; break;
                case "singleRF": case "uniform": theta=u*Math.PI/2; break;
                case "t1": return new D3(0,0,1-Math.Exp(-2*u/0.8));
                case "t2": a=Math.Exp(-0.30*u/0.10); break;
                case "mixture": a=Math.Exp(-0.30*u/(x<0?0.06:0.18)); mz=1-Math.Exp(-0.30*u/(x<0?0.5:1.2)); phase=2*Math.PI*(x<0?-8:8)*0.30*u; break;
                case "slice": theta=Math.Abs(z)<0.25?u*Math.PI/2:0; break;
                case "gx": phase=2*Math.PI*2*x*u; break;
                case "gy": phase=2*Math.PI*y*u; break;
                case "gyHold": phase=2*Math.PI*y; break;
                case "pair0": case "pair1": case "recover":
                    bool A=Math.Abs(x-0.083333333)<0.01 && Math.Abs(y+0.25)<0.01 && Math.Abs(z-0.0625)<0.01;
                    bool B=Math.Abs(x-0.083333333)<0.01 && Math.Abs(y-0.25)<0.01 && Math.Abs(z-0.0625)<0.01;
                    if(!A&&!B)return new D3(0,0,0);
                    a=A?0.8:0.3; phase=((mode=="pair1"||mode=="recover")&&!A)?Math.PI*(mode=="recover"?1:u):0; break;
                default: return new D3(0,0,1);
            }
            return new D3(a*Math.Sin(theta)*Math.Cos(phase),-a*Math.Sin(theta)*Math.Sin(phase),mz+a*Math.Cos(theta));
        }
    }
}
