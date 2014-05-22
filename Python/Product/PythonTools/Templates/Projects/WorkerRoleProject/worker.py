import os

#
# The azure library provides access to services made available by the
# Microsoft Azure platform, such as storage and messaging. 
#
# See http://go.microsoft.com/fwlink/?linkid=254360 for documentation and
# example code.
#
from azure.servicebus import ServiceBusService
from azure.storage import CloudStorageAccount

#
# The CloudStorageAccount provides factory methods for the queue, table, and
# blob services.
#
# See http://go.microsoft.com/fwlink/?linkid=246933 for Storage documentation.
#
STORAGE_ACCOUNT_NAME = '__paste_your_storage_account_name_here__'
STORAGE_ACCOUNT_KEY = '__paste_your_storage_key_here__'

if os.environ.get('EMULATED', '').lower() == 'true':
    # Running in the emulator, so use the development storage account
    storage_account = CloudStorageAccount(None, None)
else:
    storage_account = CloudStorageAccount(STORAGE_ACCOUNT_NAME, STORAGE_ACCOUNT_KEY)

blob_service = storage_account.create_blob_service()
table_service = storage_account.create_table_service()
queue_service = storage_account.create_queue_service()

#
# Service Bus is a messaging solution for applications. It sits between
# components of your applications and enables them to exchange messages in a
# loosely coupled way for improved scale and resiliency.
#
# See http://go.microsoft.com/fwlink/?linkid=246934 for Service Bus documentation.
#
SERVICE_BUS_NAMESPACE = '__paste_your_service_bus_namespace_here__'
SERVICE_BUS_KEY = '__paste_your_service_bus_key_here__'
bus_service = ServiceBusService(SERVICE_BUS_NAMESPACE, SERVICE_BUS_KEY, issuer='owner')


if __name__ == '__main__':
    while True:
        #
        # Write your worker process here.
        #
        # You will probably want to call a blocking function such as
        #    bus_service.receive_queue_message('queue name', timeout=seconds)
        # to avoid consuming 100% CPU time while your worker has no work.
        #
        pass

