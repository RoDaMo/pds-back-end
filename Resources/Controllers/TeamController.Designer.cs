﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PlayOffsApi.Resources.Controllers {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class TeamController {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal TeamController() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("PlayOffsApi.Resources.Controllers.TeamController", typeof(TeamController).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Você não tem permissão para adicionar um time participante..
        /// </summary>
        public static string AddTeamToChampionshipNoPermission {
            get {
                return ResourceManager.GetString("AddTeamToChampionshipNoPermission", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Time vinculado com sucesso.
        /// </summary>
        public static string AddTeamToChampionshipVinculado {
            get {
                return ResourceManager.GetString("AddTeamToChampionshipVinculado", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Você não tem permissão para remover um time participante..
        /// </summary>
        public static string RemoveTeamFromChampionshipNoPermissionToDelete {
            get {
                return ResourceManager.GetString("RemoveTeamFromChampionshipNoPermissionToDelete", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Time desvinculado com sucesso.
        /// </summary>
        public static string RemoveTeamFromChampionshipUnlinked {
            get {
                return ResourceManager.GetString("RemoveTeamFromChampionshipUnlinked", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Time não existente.
        /// </summary>
        public static string ShowTimeNaoExistente {
            get {
                return ResourceManager.GetString("ShowTimeNaoExistente", resourceCulture);
            }
        }
    }
}