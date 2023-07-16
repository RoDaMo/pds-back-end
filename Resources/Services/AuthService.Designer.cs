﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
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
    public class AuthService {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal AuthService() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("PlayOffsApi.Resources.Services.AuthService", typeof(AuthService).Assembly);
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
        ///   Looks up a localized string similar to CPF já cadastrado..
        /// </summary>
        public static string AddCpfUserValidationAsyncCpfCadastrado {
            get {
                return ResourceManager.GetString("AddCpfUserValidationAsyncCpfCadastrado", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Usuário já possui CPF.
        /// </summary>
        public static string AddCpfUserValidationAsyncHasCpf {
            get {
                return ResourceManager.GetString("AddCpfUserValidationAsyncHasCpf", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Confirme seu email para poder acessar sua conta..
        /// </summary>
        public static string ConfirmEmailToAccess {
            get {
                return ResourceManager.GetString("ConfirmEmailToAccess", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Erro ao enviar o email de confirmação..
        /// </summary>
        public static string ErrorSendingConfirmationEmail {
            get {
                return ResourceManager.GetString("ErrorSendingConfirmationEmail", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Email inválido..
        /// </summary>
        public static string ForgotPasswordInvalidEmail {
            get {
                return ResourceManager.GetString("ForgotPasswordInvalidEmail", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CPF inválido.
        /// </summary>
        public static string InvalidCpf {
            get {
                return ResourceManager.GetString("InvalidCpf", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Token de confirmação de email inválido..
        /// </summary>
        public static string InvalidEmailToken {
            get {
                return ResourceManager.GetString("InvalidEmailToken", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Nome de usuário ou email inválido!.
        /// </summary>
        public static string InvalidUsername {
            get {
                return ResourceManager.GetString("InvalidUsername", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Apenas jogadores podem alterar o nome artístico.
        /// </summary>
        public static string NotPlayer {
            get {
                return ResourceManager.GetString("NotPlayer", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Tente se cadastrar novamente..
        /// </summary>
        public static string TryAgain {
            get {
                return ResourceManager.GetString("TryAgain", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Senha atual incorreta.
        /// </summary>
        public static string UpdatePasswordValidationAsyncInvalidPassword {
            get {
                return ResourceManager.GetString("UpdatePasswordValidationAsyncInvalidPassword", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Email ou nome de usuário já cadastrado no sistema.
        /// </summary>
        public static string UserAlreadyRegistered {
            get {
                return ResourceManager.GetString("UserAlreadyRegistered", resourceCulture);
            }
        }
    }
}