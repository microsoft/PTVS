from django.conf.urls.defaults import patterns, include, url

# Uncomment the next two lines to enable the admin:
# from django.contrib import admin
# admin.autodiscover()

urlpatterns = patterns('',
    # Examples:
    url(r'^$', 'RollingTasksController.RollingTasksApp.views.list_tasks'),
    url(r'^list_tasks$', 'RollingTasksController.RollingTasksApp.views.list_tasks'),
    url(r'^view_results$', 'RollingTasksController.RollingTasksApp.views.view_results'),
    url(r'^add_task$', 'RollingTasksController.RollingTasksApp.views.add_task'),
    url(r'^update_task$', 'RollingTasksController.RollingTasksApp.views.update_task'),
    url(r'^delete_task$', 'RollingTasksController.RollingTasksApp.views.delete_task'),
    url(r'^save_task$', 'RollingTasksController.RollingTasksApp.views.save_task'),
)
