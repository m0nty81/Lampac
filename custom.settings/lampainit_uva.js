(function () {
    'use strict';
	
    var timer = setInterval(function(){
        if(typeof Lampa !== 'undefined'){
            clearInterval(timer);
			
            var unic_id = Lampa.Storage.get('lampac_unic_id', '');
            if (!unic_id) {
                unic_id = Lampa.Utils.uid(8).toLowerCase();
                Lampa.Storage.set('lampac_unic_id', unic_id);
            }
			
            Lampa.Utils.putScriptAsync(["{localhost}/privateinit.js?account_email="+encodeURIComponent(Lampa.Storage.get('account_email', ''))+"&uid="+encodeURIComponent(Lampa.Storage.get('lampac_unic_id', ''))], function() {});
			
            if(!Lampa.Storage.get('lampac_initiale','false')) start();
			
            window.lampa_settings.torrents_use = true;
            window.lampa_settings.demo = false;
            window.lampa_settings.read_only = false;
			
            {deny}
			
            {pirate_store}
        }
    },200);
	
    var dcma_timer = setInterval(function(){
	  if(typeof window.lampa_settings != 'undefined' && (window.lampa_settings.fixdcma || window.lampa_settings.dcma)){
		clearInterval(dcma_timer)
		if (window.lampa_settings.dcma)
			window.lampa_settings.dcma = false;
	  }
    },100);

    function start(){
        Lampa.Storage.set('lampac_initiale','true');
        Lampa.Storage.set('source','cub');
        Lampa.Storage.set('full_btn_priority','{full_btn_priority_hash}');
        Lampa.Storage.set('proxy_tmdb','{country}'=='RU');
        Lampa.Storage.set('poster_size','w500');
        
        Lampa.Storage.set('parser_use','true'); // использовать парсер
        Lampa.Storage.set('jackett_url','{jachost}');
        Lampa.Storage.set('jackett_key','1');
        Lampa.Storage.set('parser_torrent_type','jackett');
        Lampa.Storage.set('torrserver_use_link','one'); // основной адрес TS
        Lampa.Storage.set('torrserver_url','192.168.3.240:8090'); // UVA IP
        Lampa.Storage.set('torrserver_url_two','192.168.10.140:8090'); // LAR IP
        Lampa.Storage.set('torrserver_auth','false');
        Lampa.Storage.set('internal_torrclient','false');
        Lampa.Storage.set('background_type','complex'); // Сложный задник
        Lampa.Storage.set('video_quality_default','1080'); // FullHD по-умолчанию
        Lampa.Storage.set('Reloadbutton','true'); // Кнопка перезагрузки
        Lampa.Storage.set('screensaver','false'); // Выкл скринсейвера
        Lampa.Storage.set('account_use','true');
        Lampa.Storage.set('torrserver_preload','true'); // Использовать прелоадер TS
        Lampa.Storage.set('proxy_tmdb','true'); // TMDB proxy

        /*
        Lampa.Storage.set('menu_sort','["Главная","Фильмы","Сериалы","Избранное","История"]');
        Lampa.Storage.set('menu_hide','["Лента","Персона","Аниме","Релизы"]');
        Lampa.Storage.set('torrserver_url_two','192.168.10.140:8090');
        */

        var plugins = Lampa.Plugins.get();

        var plugins_add = [
			{initiale},
            {"url": "{localhost}/plugins/nc.js", "status": 1,"name": "NewCategories", "author": "x"}, // Плагин доп категорий
            {"url": "{localhost}/plugins/mult.js", "status": 1,"name": "Mult", "author": "x"}, // Плагин переименования Аниме в Мультфильмы
            {"url": "{localhost}/plugins/pubtorr.js", "status": 1,"name": "PublicParsers", "author": "x"}, // Плагин публичных парсеров
            {"url": "{localhost}/plugins/ts-preload.js", "status": 1,"name": "TorrPreload", "author": "x"}, // Плагин предзагрузки TS
            {"url": "{localhost}/backup.js", "status": 1,"name": "Backup", "author": "x"} // Плагин резервного копирования
//            {"url": "{localhost}/pirate_store.js", "status": 1,"name": "PirateStore", "author": "x"} // Плагин пиратского стора
        ];

        var plugins_push = []

        plugins_add.forEach(function(plugin){
            if(!plugins.find(function(a){ return a.url == plugin.url})){
                Lampa.Plugins.add(plugin);
                Lampa.Plugins.save();

                plugins_push.push(plugin.url)
            }
        });

        if(plugins_push.length) Lampa.Utils.putScript(plugins_push,function(){},function(){},function(){},true);

        /*
        setTimeout(function(){
            Lampa.Noty.show('Плагины установлены, перезагрузка через 5 секунд.',{time: 5000})
        },5000)
        */
    }
})();