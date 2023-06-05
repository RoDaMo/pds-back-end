﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PlayOffsApi.Resources.Services {
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
    public class PlayerService {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal PlayerService() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("PlayOffsApi.Resources.Services.PlayerService", typeof(PlayerService).Assembly);
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
        ///   Looks up a localized string similar to Jogador passado já pertence a um time..
        /// </summary>
        public static string CreateValidationAsyncAlreadyBelongsTeam {
            get {
                return ResourceManager.GetString("CreateValidationAsyncAlreadyBelongsTeam", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Já existe jogador com o número de camisa passado..
        /// </summary>
        public static string CreateValidationAsyncAlreadyExists {
            get {
                return ResourceManager.GetString("CreateValidationAsyncAlreadyExists", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Já existe capitão no time atual..
        /// </summary>
        public static string CreateValidationAsyncAlreadyHasCaptain {
            get {
                return ResourceManager.GetString("CreateValidationAsyncAlreadyHasCaptain", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Time passado não existe..
        /// </summary>
        public static string CreateValidationAsyncDoesntExist {
            get {
                return ResourceManager.GetString("CreateValidationAsyncDoesntExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Posição inválida para o esporte do time..
        /// </summary>
        public static string CreateValidationAsyncInvalidPosition {
            get {
                return ResourceManager.GetString("CreateValidationAsyncInvalidPosition", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Já existe jogador temporário com o número de camisa passado..
        /// </summary>
        public static string CreateValidationAsyncNumberAlreadyExists {
            get {
                return ResourceManager.GetString("CreateValidationAsyncNumberAlreadyExists", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Apenas técnicos podem cadastrar jogadores..
        /// </summary>
        public static string CreateValidationAsyncOnlyTechnicians {
            get {
                return ResourceManager.GetString("CreateValidationAsyncOnlyTechnicians", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Usuário passado não existe..
        /// </summary>
        public static string CreateValidationAsyncUserDoesntExist {
            get {
                return ResourceManager.GetString("CreateValidationAsyncUserDoesntExist", resourceCulture);
            }
        }
    }
}
