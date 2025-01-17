﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kokoro.Math;

namespace Kokoro.Physics.Prefabs
{
    public class Ray : ICollisionBody
    {
        public Vector3 Origin;
        public Vector3 Direction;

        public Ray(Vector3 orig, Vector3 Dir)
        {
            Origin = orig;
            Direction = Dir;
        }
    }
}
