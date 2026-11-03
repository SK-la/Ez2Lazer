// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Bridges Cubism Core export renames (5.3+: <c>csmGetRenderOrders</c>).
    /// Live2DCSharpSDK NuGet still binds the pre-5.3 <c>csmGetDrawableRenderOrders</c> name.
    /// </summary>
    internal static unsafe class EzPetCubismCoreCompat
    {
        private enum OrderApi
        {
            Unknown,
            RenderOrders,
            DrawableDrawOrders,
            None,
        }

        private static OrderApi orderApi = OrderApi.Unknown;
        private static delegate* unmanaged[Cdecl]<IntPtr, int*> orderFn;

        /// <summary>
        /// Returns per-drawable sort keys, or null to keep index order.
        /// </summary>
        public static int* TryGetDrawableSortOrders(IntPtr model)
        {
            if (orderApi == OrderApi.None)
                return null;

            if (orderApi == OrderApi.Unknown)
                resolveOrderApi();

            if (orderApi == OrderApi.None || orderFn == null)
                return null;

            return orderFn(model);
        }

        private static void resolveOrderApi()
        {
            if (EzPetCubismNative.TryGetExport("csmGetRenderOrders", out IntPtr renderOrders))
            {
                orderFn = (delegate* unmanaged[Cdecl]<IntPtr, int*>)renderOrders;
                orderApi = OrderApi.RenderOrders;
                return;
            }

            if (EzPetCubismNative.TryGetExport("csmGetDrawableDrawOrders", out IntPtr drawOrders))
            {
                orderFn = (delegate* unmanaged[Cdecl]<IntPtr, int*>)drawOrders;
                orderApi = OrderApi.DrawableDrawOrders;
                return;
            }

            orderApi = OrderApi.None;
            orderFn = null;
        }
    }
}
