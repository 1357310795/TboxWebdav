using Microsoft.AspNetCore.Server.Kestrel.Core;
using TboxWebdav.Server.Handlers;
using TboxWebdav.Server.Modules;
using TboxWebdav.Server.Modules.Tbox;
using TboxWebdav.Server.Modules.Tbox.Services;
using TboxWebdav.Server.Modules.Webdav;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;
using System.CommandLine;
using TboxWebdav.Server.AspNetCore.Models;
using TboxWebdav.Server.AspNetCore.Middlewares;

namespace TboxWebdav.Server.AspNetCore
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var configFileOption = new Option<FileInfo?>(
                aliases: new string[] { "--config", "-c" },
                description: "ָ��һ�� YAML ��ʽ�������ļ���ʹ�������ļ�ʱ�����������в���ȫ����Ч��"
            );
            var portOption = new Option<int>(
                aliases: new string[] { "--port", "-p" },
                getDefaultValue: () => 65472,
                description: "ָ�� HTTP ��������Ķ˿ںš�"
            );
            var hostOption = new Option<string>(
                aliases: new string[] { "--host", "-h" },
                getDefaultValue: () => "localhost",
                description: "ָ�� HTTP ����������������� IP ��ַ��"
            );
            var cacheSizeOption = new Option<int>(
                aliases: new string[] { "--cachesize" },
                getDefaultValue: () => 20 * 1024 * 1024,
                description: "ָ������ռ�Ĵ�С��������С�� 10MB����"
            );
            var authModeOption = new Option<AppAuthMode>(
                aliases: new string[] { "--auth" },
                getDefaultValue: () => AppAuthMode.Mixed,
                description: """
                ָ�� WebDav �������֤��ʽ��֧�ֵ�ֵ���� 'None'��'JaCookie'��'UserToken'��'Custom'��'Mixed'��
                 - None ��ʾ WebDav ����ʹ��������֤����ʱ����ָ�� --cookie ���� --token ��Ϊ���û��ռ��������֤ƾ֤��
                 - JaCookie ��ʾ WebDav ����ʹ�� jAccount �� JAAuthCookie ������֤
                 - UserToken ��ʾ WebDav ����ʹ�� ������ �� UserToken ������֤
                 - Custom ��ʾ WebDav ����ʹ���Զ����û������������֤����ʱ����ָ�� --cookie ���� --token ��Ϊ���û��ռ��������֤ƾ֤������ʹ�������ļ����и����ӵ���֤���ԡ�
                 - Mixed ��ʾ WebDav ����ʹ�û����֤��ͬʱ֧�� JaCookie �� UserToken ������֤��ʽ���������������������֧�� Custom ��֤��ʽ��
                """
            );
            var userNameOption = new Option<string?>(
                aliases: new string[] { "--username", "-U" },
                description: "ָ������ WebDav ������֤���Զ����û�����"
            );
            var passwordOption = new Option<string?>(
                aliases: new string[] { "--password", "-P" },
                description: "ָ������ WebDav ������֤���Զ������롣"
            );
            var cookieOption = new Option<string?>(
                aliases: new string[] { "--cookie", "-C" },
                description: "ָ������ jAccount ��֤�� JAAuthCookie �ַ�����"
            );
            var userTokenOption = new Option<string?>(
                aliases: new string[] { "--token", "-T" },
                description: "ָ������ ������ ��֤���û����ơ�"
            );
            var accessModeOption = new Option<AppAccessMode>(
                aliases: new string[] { "--access" },
                getDefaultValue: () => AppAccessMode.Full,
                description: "ָ������ ������ �ķ���Ȩ�ޡ�"
            );     

            var rootCommand = new RootCommand("Welcome to TboxWebdav!");
            rootCommand.AddOption(configFileOption);
            rootCommand.AddOption(portOption);
            rootCommand.AddOption(hostOption);
            rootCommand.AddOption(cacheSizeOption);
            rootCommand.AddOption(authModeOption);
            rootCommand.AddOption(userNameOption);
            rootCommand.AddOption(passwordOption);
            rootCommand.AddOption(cookieOption);
            rootCommand.AddOption(userTokenOption);
            rootCommand.AddOption(accessModeOption);

            rootCommand.SetHandler((appOptions) =>
            {
                RunApp(appOptions);
            }, new AppCmdOptionBinder(
                configFileOption,
                portOption,
                hostOption,
                cacheSizeOption,
                authModeOption,
                userNameOption,
                passwordOption,
                cookieOption,
                userTokenOption,
                accessModeOption
            ));

            await rootCommand.InvokeAsync(args);
        }
        public static void RunApp(AppCmdOption appOptions)
        {
            if (appOptions.IsError)
            {
                Console.Error.WriteLine(appOptions.Message);
                return;
            }
            AppCmdOption.Default = appOptions;

            var builder = WebApplication.CreateBuilder();

            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 20L * 1024 * 1024 * 1024;
            });

            builder.Services.AddControllers();
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddTransient<IStoreCollection, TboxStoreCollection>();
            builder.Services.AddTransient<IStoreItem, TboxStoreItem>();
            builder.Services.AddTransient<TboxUploader>();
            builder.Services.AddScoped<IStore, TboxStore>();
            builder.Services.AddScoped<IWebDavContext, WebDavContext>();
            builder.Services.AddScoped<IWebDavDispatcher, WebDavDispatcher>();
            builder.Services.AddScoped<IWebDavStoreContext, WebDavStoreContext>();
            builder.Services.AddScoped<JaCookieProvider>();
            builder.Services.AddScoped<TboxUserTokenProvider>();
            builder.Services.AddScoped<TboxService>();
            builder.Services.AddScoped<TboxSpaceInfoProvider>();
            builder.Services.AddScoped<TboxSpaceCredProvider>();
            builder.Services.AddScoped<TboxParameterResolverProvider>();
            builder.Services.AddSingleton<HttpClientFactory>();

            builder.Services.AddKeyedScoped<IWebDavHandler, GetHandler>(WebDavRequestMethods.GET.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, PropFindHandler>(WebDavRequestMethods.PROPFIND.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, HeadHandler>(WebDavRequestMethods.HEAD.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, OptionsHandler>(WebDavRequestMethods.OPTIONS.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, DeleteHandler>(WebDavRequestMethods.DELETE.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, PutHandler>(WebDavRequestMethods.PUT.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, LockHandler>(WebDavRequestMethods.LOCK.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, UnlockHandler>(WebDavRequestMethods.UNLOCK.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, CopyHandler>(WebDavRequestMethods.COPY.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, MkcolHandler>(WebDavRequestMethods.MKCOL.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, MoveHandler>(WebDavRequestMethods.MOVE.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, PropPatchHandler>(WebDavRequestMethods.PROPPATCH.ToString());

            builder.Services.AddMemoryCache(); // Singleton

            builder.WebHost.UseUrls($"http://{appOptions.Host}:{appOptions.Port}");

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {

            }
            app.UseAuthorization();
            app.UseMiddleware<MixedAuthMiddleware>();
            app.UseMiddleware<ExtraInfoMiddleware>();

            app.MapControllers();

            app.Run();
        }
    }
}
