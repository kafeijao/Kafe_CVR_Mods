using System.Collections.Generic;
using System.IO;
using ABI_RC.Core.Savior;
using UnityEngine;

namespace LibSM64
{
    public class SM64Context : MonoBehaviour
    {
        static SM64Context s_instance = null;

        List<SM64Mario> _marios = new List<SM64Mario>();
        List<SM64DynamicTerrain> _surfaceObjects = new List<SM64DynamicTerrain>();

        void Awake()
        {
            //Interop.GlobalInit( File.ReadAllBytes( Application.dataPath + "/../baserom.us.z64" ));
            Interop.GlobalInit(CVRSuperMario64.CVRSuperMario64.SuperMario64UsZ64RomBytes);
            //RefreshStaticTerrain();

            // Update context's colliders
            Interop.StaticSurfacesLoad(Misc.GetAllStaticSurfaces());
        }

        // private void Start() {
        //     // Update the ticks at 30 times a second
        //     InvokeRepeating(nameof(FunctionToCall), 0, 1f / 30f);
        // }
        //
        // private void FunctionToCall() {
        //     FakeFixedUpdate();
        //     FakeUpdate();
        // }

        void Update() {

            foreach( var o in _surfaceObjects )
                o.contextUpdate();

            foreach( var o in _marios )
                o.contextUpdate();
        }

        void FixedUpdate() {

            foreach( var o in _surfaceObjects )
                o.contextFixedUpdate();

            foreach( var o in _marios )
                o.contextFixedUpdate();
        }

        void OnApplicationQuit() {

            Interop.GlobalTerminate();
            s_instance = null;
        }

        static void ensureInstanceExists() {

            if( s_instance == null ) {

                var contextGo = new GameObject( "SM64_CONTEXT" );
                contextGo.hideFlags |= HideFlags.HideInHierarchy;
                s_instance = contextGo.AddComponent<SM64Context>();
            }
        }

        static public void RefreshStaticTerrain() {
            Interop.StaticSurfacesLoad( Utils.GetAllStaticSurfaces());
        }

        static public void RegisterMario( SM64Mario mario ) {

            ensureInstanceExists();

            if( !s_instance._marios.Contains( mario ))
                s_instance._marios.Add( mario );
        }

        static public void UnregisterMario( SM64Mario mario ) {

            if( s_instance != null && s_instance._marios.Contains( mario ))
                s_instance._marios.Remove( mario );
        }

        static public void RegisterSurfaceObject( SM64DynamicTerrain surfaceObject ) {

            ensureInstanceExists();

            if( !s_instance._surfaceObjects.Contains( surfaceObject ))
                s_instance._surfaceObjects.Add( surfaceObject );
        }

        static public void UnregisterSurfaceObject( SM64DynamicTerrain surfaceObject ) {

            if( s_instance != null && s_instance._surfaceObjects.Contains( surfaceObject ))
                s_instance._surfaceObjects.Remove( surfaceObject );
        }
    }
}
